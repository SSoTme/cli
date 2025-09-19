using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using RabbitMQ.Client.MessagePatterns;
using SassyMQ.Lib.RabbitMQ;
using SassyMQ.Lib.RabbitMQ.Payload;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System;
using SassyMQ.SSOTME.Lib.RabbitMQ;
using SSoTme.OST.Lib.SassySDK.Derived;

namespace SassyMQ.SSOTME.Lib.RMQActors
{
    public partial class SMQAccountHolder
    {
        protected override void OnReplyTo(SSOTMEPayload payload)
        {
            base.OnReplyTo(payload);

            if (payload.IsLexiconTerm(LexiconTermEnum.accountholder_ping_ssotmecoordinator))
            {
                this.ReplyProxy = new DMProxy(payload.DirectMessageQueue);
                this.AccountHolderLogin(this.ReplyProxy);
            }
            else if (payload.IsLexiconTerm(LexiconTermEnum.accountholder_login_ssotmecoordinator))
            {
                // Console.WriteLine("Connected account");
            }
        }

        public override SSOTMEPayload CreatePayload()
        {
            var payload = base.CreatePayload();
            payload.EmailAddress = "cli@aicapture.io";
            payload.Secret = "e8f398c3c1854bf7b9b0deb123ba0127";
            return payload;
        }

        public void Init(string emailAddress, string secret)
        {
            this.EmailAddress = emailAddress;
            this.Secret = secret;
            this.AccountHolderPing();
        }

    }
}

