using System;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using CoreLibrary.Extensions;

namespace AIC.Lib.DataClasses
{                            
    public partial class AICMessage
    {
        private void InitPoco()
        {
        }
        
        partial void AfterGet();
        partial void BeforeInsert();
        partial void AfterInsert();
        partial void BeforeUpdate();
        partial void AfterUpdate();

        

        
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICMessageId")]
        public String AICMessageId { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Name")]
        public String Name { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ParentMessage")]
        [RemoteIsCollection]
        public String ParentMessage { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICMessageIdentifier")]
        public String AICMessageIdentifier { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ExternalMessageIdentifier")]
        public String ExternalMessageIdentifier { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ParentAICMessageIdentifier")]
        [RemoteIsCollection]
        public String ParentAICMessageIdentifier { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ParentExternalMessageIdentifier")]
        [RemoteIsCollection]
        public String ParentExternalMessageIdentifier { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "PromptText")]
        public String PromptText { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ResponseText")]
        public String ResponseText { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ResponseSummary")]
        public String ResponseSummary { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ResponseConclusion")]
        public String ResponseConclusion { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversation")]
        [RemoteIsCollection]
        public String AICConversation { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "MessageType")]
        public String MessageType { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "IsPromptHidden")]
        public Nullable<Boolean> IsPromptHidden { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "IsResponseHidden")]
        public Nullable<Boolean> IsResponseHidden { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "PromptTokenCount")]
        public Nullable<Int32> PromptTokenCount { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ResponseTokenCount")]
        public Nullable<Int32> ResponseTokenCount { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "DateCreated")]
        public Nullable<DateTime> DateCreated { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICPricingModel")]
        [RemoteIsCollection]
        public String AICPricingModel { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "PricePer1KPromptTokens")]
        [RemoteIsCollection]
        public Nullable<decimal> PricePer1KPromptTokens { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "PRicePer1KResponseTokens")]
        [RemoteIsCollection]
        public Nullable<decimal> PRicePer1KResponseTokens { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "PromptCost")]
        public Nullable<decimal> PromptCost { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ResponseCost")]
        public Nullable<decimal> ResponseCost { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "NamefromAICConversations")]
        [RemoteIsCollection]
        public String NamefromAICConversations { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICUserName")]
        [RemoteIsCollection]
        public String AICUserName { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Sender")]
        [RemoteIsCollection]
        public String Sender { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationName")]
        [RemoteIsCollection]
        public String AICConversationName { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationOwner")]
        [RemoteIsCollection]
        public String AICConversationOwner { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationAICProject")]
        [RemoteIsCollection]
        public String AICConversationAICProject { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationAICContext")]
        [RemoteIsCollection]
        public String AICConversationAICContext { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationAICProjectName")]
        [RemoteIsCollection]
        public String AICConversationAICProjectName { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationSubject")]
        [RemoteIsCollection]
        public String AICConversationSubject { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICConversationAICContextName")]
        [RemoteIsCollection]
        public String AICConversationAICContextName { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "CustomSenderUserName")]
        public String CustomSenderUserName { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "SenderAICUserName")]
        [RemoteIsCollection]
        public String SenderAICUserName { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICSkill")]
        [RemoteIsCollection]
        public String AICSkill { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICSkillSlashCommand")]
        [RemoteIsCollection]
        public String AICSkillSlashCommand { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "CustomSlashCommand")]
        public String CustomSlashCommand { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "SlashCommand")]
        public String SlashCommand { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "CreatedMinutesAgo")]
        public Nullable<Int32> CreatedMinutesAgo { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Re")]
        [RemoteIsCollection]
        public String Re { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "AICMessageContext")]
        [RemoteIsCollection]
        public String AICMessageContext { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ResponseTextTokenEstimate")]
        public Nullable<Int32> ResponseTextTokenEstimate { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "PromptTextTokenEstimate")]
        public Nullable<Int32> PromptTextTokenEstimate { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "TotalTokens")]
        public Nullable<Int32> TotalTokens { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ConversationDate")]
        public String ConversationDate { get; set; }
    

        

        
        
        
    }
}
