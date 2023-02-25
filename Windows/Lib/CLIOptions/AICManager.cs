﻿/*******************************************
 Initially Generated by SSoT.me - codee42 & odxml42
 Created By: EJ Alexandra - 2017
             An Abstract Level, llc
 License:    Mozilla Public License 2.0
 *******************************************/
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SSoTme.OST.Lib.CLIOptions
{
    public class AICManager
    {
        public string Auth0SID { get; private set; }

        internal static AICManager Create(string auth0SID)
        {
            var aicm = new AICManager();
            aicm.Auth0SID = auth0SID;
            return aicm; 
        }

        public void Start()
        {
            var aica = new AIC.SassyMQ.Lib.SMQAICAgent("amqps://smqPublic:smqPublic@effortlessapi-rmq.ssot.me/ej-aicapture-io");
            aica.UserAICInstallReceived += Aica_UserAICInstallReceived;
            aica.UserAICReplayReceived += Aica_UserAICReplayReceived;
            aica.UserSetDataReceived += Aica_UserSetDataReceived;
            aica.UserGetDataReceived += Aica_UserGetDataReceived;

            var payload = aica.CreatePayload();
            payload.AccessToken = this.Auth0SID;
            payload.DMQueue = aica.QueueName;
            var reply = aica.MonitoringFor(payload);


            Console.WriteLine($"Listening on DMQueue: {aica.QueueName}. Press Ctrl+C to end.");
            while (!Console.KeyAvailable)
            {
                aica.WaitForComplete(1000, false);
            }
            Console.ReadKey();
            aica.Disconnect();
        }


        private void Aica_UserAICInstallReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Aica_UserGetDataReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            e.Payload.AICaptureProjectFolder = $"/{Path.GetFileName(Environment.CurrentDirectory)}";
            var found = this.LookFor("single-source-of-truth.json", e.Payload);
            if (!found) found = this.LookFor("ssot.json", e.Payload);
            if (!found) found = this.LookFor("aicapture.json", e.Payload);
        }

        private bool LookFor(string fileName, AIC.SassyMQ.Lib.StandardPayload payload)
        {
            var fi = new FileInfo(fileName);
            if (fi.Exists) return FoundFile(payload, fi);
            fi = new FileInfo(Path.Combine("ssot", fileName));
            if (fi.Exists) return FoundFile(payload, fi);
            return false;
        }

        private bool FoundFile(AIC.SassyMQ.Lib.StandardPayload payload, FileInfo fi)
        {
            payload.FileName = fi.FullName.Substring(Environment.CurrentDirectory.Length);
            payload.Content = File.ReadAllText(fi.FullName);
            return true;
        }

        private void Aica_UserSetDataReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            e.Payload.Content = $"/{Path.GetFileName(Environment.CurrentDirectory)}";
        }

        private void Aica_UserAICReplayReceived(object sender, AIC.SassyMQ.Lib.PayloadEventArgs e)
        {
            throw new Exception("Not setup to replay yet...");
        }
    }
}