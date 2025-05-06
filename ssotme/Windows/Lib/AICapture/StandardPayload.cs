using AIC.Lib.DataClasses;
using Newtonsoft.Json;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIC.SassyMQ.Lib
{
    public partial class StandardPayload
    {
        public const int DEFAULT_TIMEOUT = 30;

        public StandardPayload()
            : this(default(SMQActorBase))
        {

        }

        public StandardPayload(SMQActorBase actor)
            : this(actor, String.Empty)
        {

        }
        
        public StandardPayload(SMQActorBase actor, string content)
            : this(actor, content, true)
        {
        }
        
        public string PayloadId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string AccessToken { get; set; }
        public string Content { get; set; }
        public string[] Contents { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsHandled { get; set; }
        

        private SMQActorBase __Actor { get; set; }
        private bool TimedOutWaiting { get; set; }
        internal StandardPayload ReplyPayload { get; set; }
        private BasicDeliverEventArgs ReplyBDEA { get; set; }
        private bool ReplyRecieved { get; set; }
        
        public DateTime OnlineSince { get; set; }
        public string DMQueue { get; set; }
        public string FileName { get; set; }
        public string AICaptureProjectFolder { get; set; }
        public string[] Projects { get; set; }
        public List<TranscriptEntry> Transcripts { get; set; }
        public string AICSkillName { get; set; }
        public string README { get; set; }

        public void SetActor(SMQActorBase actor) 
        {
            this.__Actor = actor;
        }
    }
}