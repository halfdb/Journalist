using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Journalist
{
    class Client : IDisposable
    {
        public Site Site { get; set; }

        protected WebClient WebClient = new WebClient();

        public event EventHandler AccessCompleted;

        public enum State
        {
            NotInitialized,
            NotLoggedIn,
            LoggingIn,
            Ready,
            Accessing,
            Disposed
        }

        public State CurrentState { get; protected set; } = State.NotInitialized;

        [Serializable]
        public class BadStateException : Exception
        {
            public BadStateException(State state, State[] allowed)
                : base($@"Bad State: current={state}, expected={string.Join(",", allowed.Select(e => e.ToString()).ToArray())}") { }
            public BadStateException(string message) : base(message) { }
            public BadStateException(string message, Exception inner) : base(message, inner) { }
            protected BadStateException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }

        [Serializable]
        public class FailureException : Exception
        {
            public FailureException(string message) : base($"Failure while accessing. Message: {message}") { }
            public FailureException(string message, Exception inner) : base(message, inner) { }
            protected FailureException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }

        private enum TaskType
        {
            Login,
            GetJobList,
            GetJobPerPage,
            UploadJob
        }

        protected string token;
        public List<Site.Job> Jobs = new List<Site.Job>();
        protected int? selectedJodId = null;
        public int? SelectedJobId {
            get => selectedJodId;
            set
            {
                CheckState(State.Ready, State.Accessing);
                selectedJodId = value;
            }
        }

        public Client(Site site)
        {
            Site = site;
            WebClient.OpenReadCompleted += WebClientHandler;
            WebClient.UploadValuesCompleted += WebClientHandler;
            WebClient.UploadFileCompleted += WebClientHandler;

            CurrentState = State.NotLoggedIn;
        }

        private const string failureCase = "failure";
        protected void WebClientHandler(object sender, AsyncCompletedEventArgs eventArgs)
        {
            void resetState()
            {
                if (CurrentState == State.LoggingIn)
                {
                    CurrentState = State.NotLoggedIn;
                }
                else // if (CurrentState == State.Accessing)
                {
                    CurrentState = State.Ready;
                }
            }

            CheckState(State.LoggingIn, State.Accessing);

            if (eventArgs.Cancelled)
            {
                resetState();
                return;
            }
            else if (eventArgs is OpenReadCompletedEventArgs e1)
            {
#if DEBUG
                Console.WriteLine("Debug: Content received:");
                StreamReader reader = new StreamReader(e1.Result);
                Console.WriteLine(reader.ReadToEnd());
                e1.Result.Position = 0;
#endif
                DataContractJsonSerializer serializer;
                switch (e1.UserState)
                {
                    
                    case TaskType.GetJobList:
                        serializer = new DataContractJsonSerializer(typeof(Site.JobPagesResult));
                        using (var stream = e1.Result)
                        {
                            var result = (Site.JobPagesResult)serializer.ReadObject(stream);
                            if (result.Code != 0)
                            {
                                resetState();
                                throw new FailureException(result.Message);
                            }
                            pageCount = result.Count;
                        }
                        goto case nextPageCase;
                    case TaskType.GetJobPerPage:
                        serializer = new DataContractJsonSerializer(typeof(Site.JobResult));
                        using (var stream = e1.Result)
                        {
                            var result = (Site.JobResult)serializer.ReadObject(stream);
                            if (result.Code != 0)
                            {
                                resetState();
                                throw new FailureException(result.Message);
                            }
                            Jobs.AddRange(result.Jobs);
                        }
                        goto case nextPageCase;
                    case nextPageCase:
                        if (pageCount - currentPage > 0)
                        {
                            var parameters = new NameValueCollection
                            {
                                { "size", itemPerPage.ToString() },
                                { "page", currentPage.ToString() }
                            };
                            var uri = MakeGetUri(Site.JobUrl, parameters);
                            WebClient.OpenReadAsync(uri, TaskType.GetJobPerPage);
                            currentPage++;
                            return; // avoid updating state
                        }
                        else
                        {
                            currentPage = 0;
                        }
                        break;
                    default:
                        Console.WriteLine($"Warning: unexpected TaskType: {eventArgs.UserState}");
                        break;
                }
                
            }
            else if (eventArgs is UploadValuesCompletedEventArgs e2)
            {
#if DEBUG
                Console.WriteLine("Debug: Content received:");
                Console.Write(e2.Result);
#endif
                DataContractJsonSerializer serializer;
                switch (e2.UserState)
                {
                    case TaskType.Login:
                        serializer = new DataContractJsonSerializer(typeof(Site.LoginResult));
                        using (var stream = new MemoryStream(e2.Result))
                        {
                            var result = (Site.LoginResult)serializer.ReadObject(stream);
                            if (result.Code != 0)
                            {
                                resetState();
                                throw new FailureException(result.Message);
                            }
                            token = result.Token;
                            WebClient.Headers.Add("token", token);
                        }
                        break;
                    default:
                        Console.WriteLine($"Warning: unexpected TaskType: {eventArgs.UserState}");
                        break;
                }
            }
            else if (eventArgs is UploadFileCompletedEventArgs e3)
            {
#if DEBUG
                Console.WriteLine("Debug: Content received:");
                Console.Write(e3.Result);
#endif
                DataContractJsonSerializer serializer;
                switch (e3.UserState)
                {
                    case TaskType.UploadJob:
                        serializer = new DataContractJsonSerializer(typeof(Site.Result));
                        using (var stream = new MemoryStream(e3.Result))
                        {
                            var result = (Site.Result)serializer.ReadObject(stream);
                            if (result.Code != 0)
                            {
                                resetState();
                                throw new FailureException(result.Message);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                Console.WriteLine("Warning: unexpected event.");
            }

            CurrentState = State.Ready;
            AccessCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void LoginAsync(string username, string password)
        {
            CheckState(State.NotLoggedIn, State.Ready);
            var parameters = new NameValueCollection
            {
                { "phone", username },
                { "password", password }
            };
            CurrentState = State.LoggingIn;
            var uri = new Uri(Site.LoginUrl);
#if DEBUG
            Console.WriteLine($"Debug: accessing uri: {uri}");
#endif
            WebClient.UploadValuesAsync(uri, "POST", parameters, TaskType.Login);
        }

        private readonly int itemPerPage = 30;
        private int? pageCount = null;
        private int currentPage = 0;
        private const string nextPageCase = "get next page";
        public void GetJobListAsync()
        {
            CheckState(State.Ready);

            pageCount = null;
            currentPage = 0;

            var parameters = new NameValueCollection
            {
                { "size", itemPerPage.ToString() }
            };
            var uri = MakeGetUri(Site.JobPagesUrl, parameters);
#if DEBUG
            Console.WriteLine($"Debug: accessing uri: {uri}");
#endif
            WebClient.OpenReadAsync(uri, TaskType.GetJobList);
        }

        public void UploadJobAsync(string fileName)
        {
            CheckState(State.Ready);

            if (selectedJodId is int id)
            {
                var uri = new Uri(Site.GetJobUploadUrl(id));
#if DEBUG
                Console.WriteLine($"Debug: accessing uri: {uri}");
#endif
                WebClient.UploadFileAsync(uri, "POST", fileName, TaskType.UploadJob);
            }
            else
            {
                throw new BadStateException("Job not selected");
            }
        }

        private Uri MakeGetUri(string originalUrl, NameValueCollection parameters)
        {
            var builder = new StringBuilder()
                .Append(originalUrl)
                .Append("?");
            foreach (string key in parameters)
            {
                builder
                    .Append($"{key}={parameters[key]}")
                    .Append("&");
            }
            return new Uri(
                builder
                .Remove(builder.Length - 1, 1)
                .ToString()
                );
        }

        protected void CheckState(params State[] allowed)
        {
            if (!allowed.Contains(CurrentState))
            {
                throw new BadStateException(CurrentState, allowed);
            }
        }

        public void Dispose()
        {
            CurrentState = State.Disposed;
            WebClient.OpenReadCompleted -= WebClientHandler;
            WebClient.UploadValuesCompleted -= WebClientHandler;
            WebClient.UploadFileCompleted -= WebClientHandler;
            WebClient.Dispose();
        }
    }
}
