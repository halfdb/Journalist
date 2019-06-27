using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Journalist
{
    class Site
    {
        public string Scheme = "http://";
        public string Hostname = "iot.emlab.net";
        public string Root { get => Scheme + Hostname; }

        public string LoginPath = "/user/student/login";
        public string LoginUrl { get => Root + LoginPath; }

        public string JobTypePath = "/job/type";
        public string JobTypeUrl { get => Root + JobTypePath; }

        public string JobPagesPath = "/student/student/job/num";
        public string JobPagesUrl { get => Root + JobPagesPath; }

        public string JobPath = "/student/student/job";
        public string JobUrl { get => Root + JobPath; }

        public string JobUploadPathTemplate = "/student/student/{0}/upload";
        public string GetJobUploadUrl(int jobId) => Root + string.Format(JobUploadPathTemplate, jobId);

        [DataContract]
        public class Result : IExtensibleDataObject
        {
            public ExtensionDataObject ExtensionData { get; set; }
            [DataMember(Name = "msg")]
            public string Message;
            [DataMember(Name = "code")]
            public int Code;
        }

        [DataContract]
        public class LoginResult : Result
        {
            [DataMember(Name = "data")]
            public string Token;
        }

        [DataContract]
        public class JobType
        {
            [DataMember(Name = "type")]
            public string Type;

            [DataMember(Name = "name")]
            public string Name;
        }

        [DataContract]
        public class JobTypeResult : Result
        {
            [DataMember(Name = "data")]
            public List<JobType> JobTypeList;
        }

        [DataContract]
        public class JobPagesResult : Result
        {
            [DataMember(Name = "data")]
            public int Count;
        }

        /* 
         * {
         *    id:””   //作业id
         *    name:””      //作业名称
         *    type:””  //作业类型              
         *    createDateTime:””//作业发布时间
         *    expire:”” //过期天数
         *    className:””//班级名称
         * }
         */
        [DataContract]
        public class Job
        {
            [DataMember(Name = "id")]
            public int Id;
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "type")]
            public string Type;
            [DataMember(Name = "createDateTime")]
            public string CreationTime;
            [DataMember(Name = "expire")]
            public int Expire;
            [DataMember(Name = "className")]
            public string Classname;

            public override string ToString() => Name;
        }

        [DataContract]
        public class JobResult : Result
        {
            [DataMember(Name = "data")]
            public List<Job> Jobs;
        }
    }
}
