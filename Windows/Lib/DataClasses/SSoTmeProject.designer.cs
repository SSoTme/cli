using System;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using CoreLibrary.Extensions;

namespace SSoTme.OST.Lib.DataClasses
{                            
    public partial class SSoTmeProject
    {
        private void InitPoco()
        {
            
            this.SSoTmeProjectId = Guid.NewGuid().ToString();
            
                this.ProjectSettings = new BindingList<ProjectSetting>();
            
                this.ProjectTranspilers = new BindingList<ProjectTranspiler>();
            

        }
        
        partial void AfterGet();
        partial void BeforeInsert();
        partial void AfterInsert();
        partial void BeforeUpdate();
        partial void AfterUpdate();

        

        
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "SSoTmeProjectId")]
        public String SSoTmeProjectId { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Name")]
        public String Name { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "Description")]
        public String Description { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "CreatedOn")]
        public Nullable<DateTime> CreatedOn { get; set; }
    
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "RootPath")]
        public String RootPath { get; set; }
    

        
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ProjectSettings")]
        public BindingList<ProjectSetting> ProjectSettings { get; set; }
            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, PropertyName = "ProjectTranspilers")]
        public BindingList<ProjectTranspiler> ProjectTranspilers { get; set; }
            

        
        
        
    }
}
