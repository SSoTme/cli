import asyncio
import pyairtable
from pyairtable.formulas import match
import logging
import time
import os
import argparse
from datetime import datetime


logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')


class AirtableBaseConnector:
    def __init__(self, base_id, airtable_pat):
        self.base_id = base_id
        self.airtable_pat = airtable_pat
        self.api = pyairtable.Api(airtable_pat)
        self.timeout_seconds = 30  # 30 second timeout for all operations
        self.max_retries = 3
        self.retry_delay = 2  # seconds
        self.circuit_breaker_failures = 0
        self.circuit_breaker_threshold = 5
        self.circuit_breaker_reset_time = 300  # 5 minutes
        self.circuit_breaker_last_failure = None
        self.is_circuit_open = False

    def _check_circuit_breaker(self):
        """Check if circuit breaker should be opened or closed."""
        if self.is_circuit_open:
            # Check if enough time has passed to attempt reset
            if (self.circuit_breaker_last_failure and
                    time.time() - self.circuit_breaker_last_failure > self.circuit_breaker_reset_time):
                self.is_circuit_open = False
                self.circuit_breaker_failures = 0
                logger.info("Circuit breaker reset - attempting Airtable connection")
                return False
            return True
        return False

    def _record_failure(self):
        """Record a failure and potentially open circuit breaker."""
        self.circuit_breaker_failures += 1
        self.circuit_breaker_last_failure = time.time()

        if self.circuit_breaker_failures >= self.circuit_breaker_threshold:
            self.is_circuit_open = True
            logger.error(f"Circuit breaker opened after {self.circuit_breaker_failures} failures")

    def _record_success(self):
        """Record a successful operation."""
        self.circuit_breaker_failures = 0
        self.is_circuit_open = False

    async def _execute_with_timeout_and_retry(self, operation, operation_name: str):
        """Execute an Airtable operation with timeout, retry, and circuit breaker."""
        if self._check_circuit_breaker():
            raise Exception(f"Circuit breaker is open for Airtable operations (too many failures)")

        last_exception = None

        for attempt in range(self.max_retries):
            try:
                # Execute with timeout
                result = await asyncio.wait_for(
                    asyncio.to_thread(operation),
                    timeout=self.timeout_seconds
                )
                self._record_success()
                return result

            except asyncio.TimeoutError:
                last_exception = Exception(f"Airtable operation '{operation_name}' timed out after {self.timeout_seconds}s")
                logger.warning(f"Timeout on attempt {attempt + 1}/{self.max_retries}: {operation_name}")

            except Exception as e:
                last_exception = e
                logger.warning(f"Error on attempt {attempt + 1}/{self.max_retries} for {operation_name}: {e}")

                # Don't retry on certain types of errors
                if "401" in str(e) or "authentication" in str(e).lower():
                    break

            # Wait before retry (except on last attempt)
            if attempt < self.max_retries - 1:
                await asyncio.sleep(self.retry_delay * (attempt + 1))  # Exponential backoff

        # All retries failed
        self._record_failure()
        logger.error(f"All {self.max_retries} attempts failed for {operation_name}: {last_exception}")
        raise last_exception

    async def _get_table_contents(self, table_name: str):
        return await self._execute_with_timeout_and_retry(
            lambda: self.api.table(self.base_id, table_name).all(),
            f"get_table_contents({table_name})"
        )

    async def get_last_row_of_table(self, table_name: str, sort_by_field_name: str):
        contents = await self._get_table_contents(table_name)
        if not contents:
            return None
        contents.sort(key=lambda r: r['fields'].get(sort_by_field_name, ''))
        return contents[-1]['fields']
    
    async def create_record(self, table_name: str, fields: dict):
        """Create a new record in the specified table."""
        return await self._execute_with_timeout_and_retry(
            lambda: self.api.table(self.base_id, table_name).create(fields),
            f"create_record({table_name})"
        )


AIRTABLE_PAT = os.getenv("AIRTABLE_PAT")
SSOT_BASE_ID = os.getenv("SSOT_BASE_ID")


def generate_installer_urls(version: str, github_repo: str) -> dict:
    """Generate installer download URLs for all platforms."""
    base_url = f"https://github.com/{github_repo}/releases/download/v{version}"
    return {
        'WindowsInstaller': f"{base_url}/SSoTme-Installer_win-x64.msi",
        'WindowsArmInstaller': f"{base_url}/SSoTme-Installer_win-arm64.msi", 
        'MacInstaller': f"{base_url}/SSoTme-Installer-x86_64.pkg",
        'MacArmInstaller': f"{base_url}/SSoTme-Installer-arm64.pkg"
    }


async def main():
    parser = argparse.ArgumentParser(description='Add latest version to Airtable CLIVersions table')
    parser.add_argument('--version', required=True, help='Version to add (e.g., 2025.08.14)')
    args = parser.parse_args()
    
    if not AIRTABLE_PAT:
        logger.error("AIRTABLE_PAT environment variable is required")
        return 1
    
    if not SSOT_BASE_ID:
        logger.error("SSOT_BASE_ID environment variable is required")
        return 1
    
    github_repo = os.getenv("GITHUB_REPOSITORY", "SSoTme/cli")  # fallback to default
    
    try:
        # Initialize Airtable connector
        connector = AirtableBaseConnector(SSOT_BASE_ID, AIRTABLE_PAT)
        table_name = "CLIVersions"
        
        logger.info(f"Checking for existing version: {args.version}")
        
        # Check if version already exists
        last_row = await connector.get_last_row_of_table(table_name, "Date")
        
        if last_row and last_row.get("Version") == args.version:
            logger.info(f"Version {args.version} already exists in Airtable - skipping")
            return 0
        
        logger.info(f"Creating new record for version: {args.version}")
        
        # Generate installer URLs
        installer_urls = generate_installer_urls(args.version, github_repo)
        
        # Create new record
        current_date = datetime.now().strftime('%Y-%m-%d')
        release_index = datetime.now().strftime('%Y%m%d%H%M%S')  # timestamp-based index
        
        fields = {
            'Name': f"Release v{args.version}",
            'Description': f"CLI Release {args.version} - Cross-platform installers for SSoTme",
            'Date': current_date,
            'Version': args.version,
            'ReleaseIndex': release_index,
            'InstallerId': f"cli-{args.version}-{release_index}",
            **installer_urls
        }
        
        result = await connector.create_record(table_name, fields)
        logger.info(f"Successfully created record with ID: {result['id']}")
        
        # Log the URLs for verification
        logger.info("Generated installer URLs:")
        for platform, url in installer_urls.items():
            logger.info(f"  {platform}: {url}")
        
        return 0
        
    except Exception as e:
        logger.error(f"Failed to update Airtable: {e}")
        return 1


if __name__ == '__main__':
    exit_code = asyncio.run(main())
    exit(exit_code)
