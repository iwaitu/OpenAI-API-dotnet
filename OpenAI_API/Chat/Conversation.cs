using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI_API.ChatFunctions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace OpenAI_API.Chat
{
	/// <summary>
	/// Represents on ongoing chat with back-and-forth interactions between the user and the chatbot.  This is the simplest way to interact with the ChatGPT API, rather than manually using the ChatEnpoint methods.  You do lose some flexibility though.
	/// </summary>
	public class Conversation
	{
		/// <summary>
		/// An internal reference to the API endpoint, needed for API requests
		/// </summary>
		private ChatEndpoint _endpoint;

		/// <summary>
		/// Allows setting the parameters to use when calling the ChatGPT API.  Can be useful for setting temperature, presence_penalty, and more.  <see href="https://platform.openai.com/docs/api-reference/chat/create">Se  OpenAI documentation for a list of possible parameters to tweak.</see>
		/// </summary>
		public ChatRequest RequestParameters { get; private set; }

        public LLamaChatRequest LLamaRequestParameters { get; private set; }

        public GemmaChatRequest GemmaRequestParameters { get; private set; }
        public QwenChatRequest QwenRequestParameters { get; private set; }

        /// <summary>
        /// Specifies the model to use for ChatGPT requests.  This is just a shorthand to access <see cref="RequestParameters"/>.Model
        /// </summary>
        public OpenAI_API.Models.Model Model
		{
			get
			{
				return RequestParameters.Model;
			}
			set
			{
				RequestParameters.Model = value;
			}
		}

		/// <summary>
		/// After calling <see cref="GetResponseFromChatbotAsync"/>, this contains the full response object which can contain useful metadata like token usages, <see cref="ChatChoice.FinishReason"/>, etc.  This is overwritten with every call to <see cref="GetResponseFromChatbotAsync"/> and only contains the most recent result.
		/// </summary>
		public ChatResult MostRecentApiResult { get; private set; }

		/// <summary>
		/// Creates a new conversation with ChatGPT chat
		/// </summary>
		/// <param name="endpoint">A reference to the API endpoint, needed for API requests.  Generally should be <see cref="OpenAIAPI.Chat"/>.</param>
		/// <param name="model">Optionally specify the model to use for ChatGPT requests.  If not specified, used <paramref name="defaultChatRequestArgs"/>.Model or falls back to <see cref="OpenAI_API.Models.Model.ChatGPTTurbo"/></param>
		/// <param name="defaultChatRequestArgs">Allows setting the parameters to use when calling the ChatGPT API.  Can be useful for setting temperature, presence_penalty, and more.  See <see href="https://platform.openai.com/docs/api-reference/chat/create">OpenAI documentation for a list of possible parameters to tweak.</see></param>
		public Conversation(ChatEndpoint endpoint, OpenAI_API.Models.Model model = null, ChatRequest defaultChatRequestArgs = null)
		{
			RequestParameters = new ChatRequest(defaultChatRequestArgs);
			if (model != null)
				RequestParameters.Model = model;
			if (RequestParameters.Model == null)
				RequestParameters.Model = Models.Model.ChatGPTTurbo;

			_Messages = new List<ChatMessage>();
			_endpoint = endpoint;
			RequestParameters.NumChoicesPerMessage = 1;
			RequestParameters.Stream = false;
		}

        public Conversation(ChatEndpoint endpoint, OpenAI_API.Models.Model model = null, LLamaChatRequest defaultChatRequestArgs = null)
        {
            LLamaRequestParameters = new LLamaChatRequest(defaultChatRequestArgs);
            if (model != null)
                LLamaRequestParameters.Model = model;
            if (LLamaRequestParameters.Model == null)
                LLamaRequestParameters.Model = Models.Model.ChatGPTTurbo;

            _Messages = new List<ChatMessage>();
            _endpoint = endpoint;
            LLamaRequestParameters.NumChoicesPerMessage = 1;
            LLamaRequestParameters.Stream = false;
        }

        public Conversation(ChatEndpoint endpoint, OpenAI_API.Models.Model model = null, QwenChatRequest defaultChatRequestArgs = null)
        {
            QwenRequestParameters = new QwenChatRequest(defaultChatRequestArgs);
            if (model != null)
                QwenRequestParameters.Model = model;
            if (QwenRequestParameters.Model == null)
                QwenRequestParameters.Model = Models.Model.Qwen_25;

            _Messages = new List<ChatMessage>();
            _endpoint = endpoint;
            QwenRequestParameters.NumChoicesPerMessage = 1;
            QwenRequestParameters.Stream = false;
        }

        public Conversation(ChatEndpoint endpoint, OpenAI_API.Models.Model model = null, GemmaChatRequest defaultChatRequestArgs = null)
        {
            GemmaRequestParameters = new GemmaChatRequest(defaultChatRequestArgs);
            if (model != null)
                GemmaRequestParameters.Model = model;
            if (GemmaRequestParameters.Model == null)
                GemmaRequestParameters.Model = Models.Model.ChatGPTTurbo;

            _Messages = new List<ChatMessage>();
            _endpoint = endpoint;
            GemmaRequestParameters.NumChoicesPerMessage = 1;
            GemmaRequestParameters.Stream = false;
        }

        /// <summary>
        /// A list of messages exchanged so far.  Do not modify this list directly.  Instead, use <see cref="AppendMessage(ChatMessage)"/>, <see cref="AppendUserInput(string)"/>, <see cref="AppendSystemMessage(string)"/>, or <see cref="AppendExampleChatbotOutput(string)"/>.
        /// </summary>
        public IReadOnlyList<ChatMessage> Messages { get => _Messages; }
		private List<ChatMessage> _Messages;

		/// <summary>
		/// Appends a <see cref="ChatMessage"/> to the chat history
		/// </summary>
		/// <param name="message">The <see cref="ChatMessage"/> to append to the chat history</param>
		public void AppendMessage(ChatMessage message)
		{
			_Messages.Add(message);
		}

		/// <summary>
		/// Creates and appends a <see cref="ChatMessage"/> to the chat history
		/// </summary>
		/// <param name="role">The <see cref="ChatMessageRole"/> for the message.  Typically, a conversation is formatted with a system message first, followed by alternating user and assistant messages.  See <see href="https://platform.openai.com/docs/guides/chat/introduction">the OpenAI docs</see> for more details about usage.</param>
		/// <param name="content">The content of the message)</param>
		public void AppendMessage(ChatMessageRole role, string content) => this.AppendMessage(new ChatMessage(role, content));

		/// <summary>
		/// Creates and appends a <see cref="ChatMessage"/> to the chat history with the Role of <see cref="ChatMessageRole.User"/>.  The user messages help instruct the assistant. They can be generated by the end users of an application, or set by a developer as an instruction.
		/// </summary>
		/// <param name="content">Text content generated by the end users of an application, or set by a developer as an instruction</param>
		public void AppendUserInput(string content) => this.AppendMessage(new ChatMessage(ChatMessageRole.User, content));

		/// <summary>
		/// Creates and appends a <see cref="ChatMessage"/> to the chat history with the Role of <see cref="ChatMessageRole.User"/>.  The user messages help instruct the assistant. They can be generated by the end users of an application, or set by a developer as an instruction.
		/// </summary>
		/// <param name="userName">The name of the user in a multi-user chat</param>
		/// <param name="content">Text content generated by the end users of an application, or set by a developer as an instruction</param>
		public void AppendUserInputWithName(string userName, string content) => this.AppendMessage(new ChatMessage(ChatMessageRole.User, content) { Name = userName });


		/// <summary>
		/// Creates and appends a <see cref="ChatMessage"/> to the chat history with the Role of <see cref="ChatMessageRole.System"/>.  The system message helps set the behavior of the assistant.
		/// </summary>
		/// <param name="content">text content that helps set the behavior of the assistant</param>
		public void AppendSystemMessage(string content) => this.AppendMessage(new ChatMessage(ChatMessageRole.System, content));
		/// <summary>
		/// Creates and appends a <see cref="ChatMessage"/> to the chat history with the Role of <see cref="ChatMessageRole.Assistant"/>.  Assistant messages can be written by a developer to help give examples of desired behavior.
		/// </summary>
		/// <param name="content">Text content written by a developer to help give examples of desired behavior</param>
		public void AppendExampleChatbotOutput(string content) => this.AppendMessage(new ChatMessage(ChatMessageRole.Assistant, content));
        /// <summary>
        /// openai，chatglm3 专用，创建并附加一个<see cref="ChatMessage"/>到聊天历史记录，角色为<see cref="ChatMessageRole.Function"/>。function call 后返回的结果。
		/// llama3 模型时，则用来添加一条function call 声明。因为 llama3 模型的Message数量必须为2的倍数，所以执行function call 需要按顺序先添加一条声明，再添加一条返回结果。
        /// </summary>
        /// <param name="functionName">The name of the function for which the content has been generated as the result</param>
        /// <param name="content">The text content (usually JSON)</param>
        public void AppendFunctionMessage(string functionName, string content) => AppendMessage(new ChatMessage(ChatMessageRole.Function, content) { Name = functionName });
        /// <summary>
		/// llama3 专用，创建并附加一个<see cref="ChatMessage"/>到聊天历史记录，角色为<see cref="ChatMessageRole.Tool"/>。工具消息是执行function call 后返回的结果。
		/// </summary>
		/// <param name="functionName"></param>
		/// <param name="content"></param>
		public void AppendToolMessage(string functionName, string content) => AppendMessage(new ChatMessage(ChatMessageRole.Tool, content) { Name = functionName });



        #region Non-streaming

        /// <summary>
        /// Calls the API to get a response, which is appended to the current chat's <see cref="Messages"/> as an <see cref="ChatMessageRole.Assistant"/> <see cref="ChatMessage"/>.
        /// </summary>
        /// <returns>The string of the response from the chatbot API</returns>
        public async Task<string> GetResponseFromChatbotAsync()
		{
			ChatRequest req = new ChatRequest(RequestParameters);
			req.Messages = _Messages.ToList();

			var res = await _endpoint.CreateChatCompletionAsync(req);
			MostRecentApiResult = res;

			if (res.Choices.Count > 0)
			{
				var newMsg = res.Choices[0].Message;
				AppendMessage(newMsg);
				return newMsg.Content;
			}
			return null;
		}

        public async Task<string> GetResponseFromLLamaChatbotAsync()
        {
            LLamaChatRequest req = new LLamaChatRequest(LLamaRequestParameters);
            req.Messages = _Messages.ToList();

            var res = await _endpoint.CreateChatCompletionAsync(req);
            MostRecentApiResult = res;

            if (res.Choices.Count > 0)
            {
                var newMsg = res.Choices[0].Message;
                AppendMessage(newMsg);
                return newMsg.Content;
            }
            return null;
        }

        public async Task<string> GetResponseFromGemmaChatbotAsync()
        {
            GemmaChatRequest req = new GemmaChatRequest(GemmaRequestParameters);
            req.Messages = _Messages.ToList();

            var res = await _endpoint.CreateChatCompletionAsync(req);
            MostRecentApiResult = res;

            if (res.Choices.Count > 0)
            {
                return res.Choices[0].Message.Content;
            }
            return null;
        }

        /// <summary>
        /// OBSOLETE: GetResponseFromChatbot() has been renamed to <see cref="GetResponseFromChatbotAsync"/> to follow .NET naming guidelines.  This alias will be removed in a future version.
        /// </summary>
        /// <returns>The string of the response from the chatbot API</returns>
        [Obsolete("Conversation.GetResponseFromChatbot() has been renamed to GetResponseFromChatbotAsync to follow .NET naming guidelines.  Please update any references to GetResponseFromChatbotAsync().  This alias will be removed in a future version.", false)]
		public Task<string> GetResponseFromChatbot() => GetResponseFromChatbotAsync();


		#endregion

		#region Streaming

		/// <summary>
		/// Calls the API to get a response, which is appended to the current chat's <see cref="Messages"/> as an <see cref="ChatMessageRole.Assistant"/> <see cref="ChatMessage"/>, and streams the results to the <paramref name="resultHandler"/> as they come in. <br/>
		/// If you are on the latest C# supporting async enumerables, you may prefer the cleaner syntax of <see cref="StreamResponseEnumerableFromChatbotAsync"/> instead.
		///  </summary>
		/// <param name="resultHandler">An action to be called as each new result arrives.</param>
		public async Task StreamResponseFromChatbotAsync(Action<string> resultHandler)
		{
			await foreach (string res in StreamResponseEnumerableFromChatbotAsync())
			{
				resultHandler(res);
			}
		}

		/// <summary>
		/// Calls the API to get a response, which is appended to the current chat's <see cref="Messages"/> as an <see cref="ChatMessageRole.Assistant"/> <see cref="ChatMessage"/>, and streams the results to the <paramref name="resultHandler"/> as they come in. <br/>
		/// If you are on the latest C# supporting async enumerables, you may prefer the cleaner syntax of <see cref="StreamResponseEnumerableFromChatbotAsync"/> instead.
		///  </summary>
		/// <param name="resultHandler">An action to be called as each new result arrives, which includes the index of the result in the overall result set.</param>
		public async Task StreamResponseFromChatbotAsync(Action<int, string> resultHandler)
		{
			int index = 0;
			await foreach (string res in StreamResponseEnumerableFromChatbotAsync())
			{
				resultHandler(index++, res);
			}
		}

		/// <summary>
		/// Calls the API to get a response, which is appended to the current chat's <see cref="Messages"/> as an <see cref="ChatMessageRole.Assistant"/> <see cref="ChatMessage"/>, and streams the results as they come in. <br/>
		/// If you are not using C# 8 supporting async enumerables or if you are using the .NET Framework, you may need to use <see cref="StreamResponseFromChatbotAsync(Action{string})"/> instead.
		/// </summary>
		/// <returns>An async enumerable with each of the results as they come in.  See <see href="https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams"/> for more details on how to consume an async enumerable.</returns>
		public async IAsyncEnumerable<string> StreamResponseEnumerableFromChatbotAsync()
		{
			ChatRequest req = new ChatRequest(RequestParameters);
			req.Messages = _Messages.ToList();

			StringBuilder responseStringBuilder = new StringBuilder();
			ChatMessageRole responseRole = null;
			bool setValue = false;
			MostRecentApiResult = null;
			await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
			{
				if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
				{
					if (delta.Role != null)
						responseRole = delta.Role;

					string deltaContent = delta.Content;

					if (!string.IsNullOrEmpty(deltaContent))
					{
						responseStringBuilder.Append(deltaContent);
						yield return deltaContent;
					}
					else
					{
						if (!setValue)
						{
                            MostRecentApiResult = res;
							setValue = true;
						}
						else
						{
							if (delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
							{
								if(MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                                    MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

								MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
                            }
							
							if(res.Choices.FirstOrDefault()?.FinishReason != null)
							{
								MostRecentApiResult.Choices.FirstOrDefault().FinishReason = res.Choices.FirstOrDefault()?.FinishReason;
                            }
							
						}
						
					}
					
				}
			}

			if (responseRole != null && responseStringBuilder.Length > 0)
			{
				AppendMessage(responseRole, responseStringBuilder.ToString());
			}
		}

        
        /// <summary>
        /// 从输入字符串中提取 <tool_call> 标签之间的内容，
        /// 并解析 JSON，提取 "name" 和 "arguments" 字段。
        /// 如果匹配或解析失败，则返回 (null, null)。
        /// </summary>
        /// <param name="inputStr">输入字符串</param>
        /// <returns>元组 (name, arguments)；若失败返回 (null, null)</returns>
        private FunctionCall ProcessStreamChunkQwen(string inputStr)
        {
            var pattern = @"<tool_call>(.*?)</tool_call>";
            var match = Regex.Match(inputStr, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                string content = match.Groups[1].Value;

                try
                {
                    // 尝试将内容解析为 JSON 对象
                    var jsonContent = JObject.Parse(content);

                    // 提取 "name" 和 "arguments"
                    JToken nameToken = jsonContent["name"];
                    JToken argumentsToken = jsonContent["arguments"];

                    if (nameToken != null && argumentsToken != null)
                    {
                        string name = nameToken.ToString();

                        // 将 arguments 转为 JSON 字符串，不转义非 ASCII 字符
                        var settings = new JsonSerializerSettings
                        {
                            StringEscapeHandling = StringEscapeHandling.Default
                        };
                        string arguments = JsonConvert.SerializeObject(argumentsToken, settings);

                        return new FunctionCall { Name = name, Arguments = arguments };
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (JsonReaderException)
                {
                    // JSON 解析失败
                    return null;
                }
            }

            // 未匹配到 <tool_call> 标签时返回 null
            return null;
        }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromR1ChatbotAsync()
        {
            var req = new ChatRequest(RequestParameters);
            req.Messages = _Messages.ToList();
            StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            string buffer_msg = string.Empty;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    if (!string.IsNullOrEmpty(delta.ReasoningContent))
                    {
                        MostRecentApiResult.Choices.FirstOrDefault().Delta.Thinking = true;
                        MostRecentApiResult.Choices.FirstOrDefault().Delta.ReasoningContent += delta.ReasoningContent;
                        yield return delta.ReasoningContent;
                    } else if (!string.IsNullOrEmpty(delta.Content))
                    {
                        if(MostRecentApiResult.Choices.FirstOrDefault().Delta.Thinking)
                        {
                            if (!string.IsNullOrEmpty(MostRecentApiResult.Choices.FirstOrDefault().Delta.ReasoningContent))
                            {
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.Thinking = false;
                            }
                        }
                        MostRecentApiResult.Choices.FirstOrDefault().Delta.Content += delta.Content;
                        yield return delta.Content;
                    }
                    else
                    {
                        if (!setValue)
                        {
                            MostRecentApiResult = res;
                            setValue = true;
                        }
                    }
                }
            }
            if (responseRole != null && responseStringBuilder.Length > 0)
            {
                AppendMessage(responseRole, responseStringBuilder.ToString());
            }
        }

        /// <summary>
        /// 基于vllm 的qwen 模型，从聊天机器人获取响应，并将结果流式传递。支持stream function call
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<string> StreamResponseEnumerableFromQwenChatbotAsync()
        {
            var req = new QwenChatRequest(QwenRequestParameters);
            req.Messages = _Messages.ToList();
            

            StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            string buffer_msg = string.Empty;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    string deltaContent = delta.Content;
                    buffer_msg += string.IsNullOrEmpty(deltaContent) ? "" : deltaContent;
                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        if (buffer_msg.StartsWith("<tool_call>"))
                        {
                            var ret = ProcessStreamChunkQwen(buffer_msg);
                            if (ret!= null && MostRecentApiResult != null)
                            {
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = ret;
                                MostRecentApiResult.Choices.FirstOrDefault().FinishReason = "function_call";
                                yield return "";
                            }
                            continue;
                        }
                        responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                    if (!setValue)
                    {
                        MostRecentApiResult = res;
                        setValue = true;
                    }

                }
            }

            if (responseRole != null && responseStringBuilder.Length > 0)
            {
                AppendMessage(responseRole, responseStringBuilder.ToString());
            }
        }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromAliQwenChatbotAsync()
        {
            var req = new QwenChatRequest(QwenRequestParameters);
            req.Messages = _Messages.ToList();


            StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (!setValue)
                {
                    MostRecentApiResult = res;
                    setValue = true;
                }

                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    string deltaContent = delta.Content;
                    if (delta.ToolCalls?.FirstOrDefault()?.Function != null && delta.ToolCalls?.FirstOrDefault()?.Function.Arguments != null )
                    {
                        if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                            MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

                        MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.ToolCalls?.FirstOrDefault()?.Function.Arguments;
                        if (delta.ToolCalls?.FirstOrDefault()?.Function.Name != null)
                        {
                            MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name = delta.ToolCalls?.FirstOrDefault()?.Function.Name;
                        }
                        MostRecentApiResult.Choices.FirstOrDefault().FinishReason = "function_call";

                    }

                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                }
            }

            if (responseRole != null && responseStringBuilder.Length > 0)
            {
                AppendMessage(responseRole, responseStringBuilder.ToString());
            }
        }

        /// <summary>
        /// 基于vllm 的qwen 模型，从聊天机器人获取响应，并将结果流式传递。支持stream function call
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<string> StreamResponseEnumerableFromQwQChatbotAsync()
        {
            var req = new QwenChatRequest(QwenRequestParameters);
            req.Messages = _Messages.ToList();
            
            //StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            string buffer_msg = string.Empty;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    string deltaContent = delta.Content;
                    if(buffer_msg.Length == 0 && MostRecentApiResult != null)
                    {
                        MostRecentApiResult.Choices.FirstOrDefault().Delta.Thinking = true;
                    }
                    buffer_msg += string.IsNullOrEmpty(deltaContent) ? "" : deltaContent;
                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        
                        int index = buffer_msg.IndexOf("<tool_call>");
                        if (index>=0)
                        {
                            var ret = ProcessStreamChunkQwen(buffer_msg.Substring(index));
                            if (ret != null && MostRecentApiResult != null)
                            {
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = ret;
                                MostRecentApiResult.Choices.FirstOrDefault().FinishReason = "function_call";
                                yield return "";
                            }
                            continue;
                        }
                        if(deltaContent == "</think>")
                        {
                            MostRecentApiResult.Choices.FirstOrDefault().Delta.Thinking = false;
                            yield return "";
                            continue;
                        }
                        if (MostRecentApiResult.Choices.FirstOrDefault().Delta.Thinking)
                        {

                            MostRecentApiResult.Choices.FirstOrDefault().Delta.ReasoningContent += deltaContent;

                        }
                        else
                        {
                            MostRecentApiResult.Choices.FirstOrDefault().Delta.Content += deltaContent;
                        }
                         //responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                    else
                    {
                        if (!setValue)
                        {
                            MostRecentApiResult = res;
                            setValue = true;
                        }
                    }

                }
            }

        }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromGemmaChatbotAsync(string functionToken = "```\nAction:")
        {
            var req = new GemmaChatRequest(GemmaRequestParameters);
            req.Messages = _Messages.ToList();

            StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            var buffer_msg = string.Empty;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    string deltaContent = delta.Content;
                    if(buffer_msg.Length < functionToken.Length)
                        buffer_msg += string.IsNullOrEmpty(deltaContent) ? "" : deltaContent;

                    if (!string.IsNullOrEmpty(deltaContent) && !buffer_msg.StartsWith("```") && !buffer_msg.StartsWith("```\n") && !buffer_msg.StartsWith("```\nAction") && !buffer_msg.StartsWith(functionToken))
                    {
                        responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                    else
                    {
                        if (!setValue)
                        {
                            MostRecentApiResult = res;
                            setValue = true;
                        }
                        else
                        {
                            if (delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
                            {
                                if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                                    MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
                            }

                            if (res.Choices.FirstOrDefault()?.FinishReason != null)
                            {
                                MostRecentApiResult.Choices.FirstOrDefault().FinishReason = res.Choices.FirstOrDefault()?.FinishReason;
                            }

                        }

                    }

                }
            }

            //if (responseRole != null && responseStringBuilder.Length > 0)
            //{
            //    AppendMessage(responseRole, responseStringBuilder.ToString());
            //}
        }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromLLamaChatbotAsync(string[] functionTokens = null)
        {
            var req = new LLamaChatRequest(LLamaRequestParameters);
            req.Messages = _Messages.ToList();

            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            var buffer_msg = string.Empty;
            var functionDetected = false;
            var cacheStarted = false;
            
            // Define default function tokens if none are provided
            functionTokens ??= new[] { "Action:","```\nAction:", "```tool_call\nAction:", "```tool_code\nAction:", "```python\nAction:" };
            var maxFunctionTokenLength = functionTokens?.Max(ft => ft.Length) ?? 0;

            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    if (!setValue)
                    {
                        MostRecentApiResult = res;
                        setValue = true;
                    }

                    string deltaContent = delta.Content;
                    if(res.Choices.FirstOrDefault()?.FinishReason == "function_call")
                    {
                        functionDetected = true;
                    }
                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        // Check if caching should start (if we detect a newline in deltaContent)
                        if (deltaContent.Contains("\n") || deltaContent == "```" || deltaContent == "```\n" || deltaContent == "Action")
                        {
                            cacheStarted = true;
                        }

                        buffer_msg += deltaContent;

                        if (cacheStarted)
                        {
                            // Start checking for function tokens only if caching has started
                            foreach (var functionToken in functionTokens)
                            {
                                if (buffer_msg.Contains(functionToken) && !functionDetected)
                                {
                                    // Split the buffer around the functionToken
                                    var parts = buffer_msg.Split(new[] { functionToken }, 2, StringSplitOptions.None);

                                    // Output the part before the functionToken if it exists
                                    if (!string.IsNullOrEmpty(parts[0]) && parts[0]!="```\n")
                                    {
                                        yield return parts[0];
                                    }

                                    // Mark functionToken detected and update the buffer with the remaining part after the functionToken
                                    functionDetected = true;
                                    buffer_msg = parts.Length > 1 ? parts[1] : string.Empty;

                                    break; // Exit the loop once a function token is detected
                                }
                            }

                            // If buffer_msg exceeds the length of the longest functionToken and no functionToken is detected
                            if (!functionDetected && buffer_msg.Length > maxFunctionTokenLength)
                            {
                                // Wait for the next newline to push the buffer
                                if (deltaContent.Contains("\n"))
                                {
                                    yield return buffer_msg;
                                    buffer_msg = string.Empty;
                                    cacheStarted = false;

                                }
                            }
                        }
                        else
                        {
                            // Output content directly if caching hasn't started
                            yield return buffer_msg;
                            buffer_msg = string.Empty;
                        }

                        // Handle function call accumulation
                        if (functionDetected && delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
                        {
                            if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

                            MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
                            MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
                        }

                        if (functionDetected && res.Choices.FirstOrDefault()?.FinishReason != null)
                        {
                            MostRecentApiResult.Choices.FirstOrDefault().FinishReason = "function_call";
                        }
                    }
                }
            }

            // Handle any remaining content after processing the stream
            if (!functionDetected && buffer_msg.Length > 0)
            {
                yield return buffer_msg;
            }
        }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromQwenChatbotAsync(string[] functionTokens = null)
        {
            var req = new LLamaChatRequest(LLamaRequestParameters);
            req.Messages = _Messages.ToList();

            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            var buffer_msg = string.Empty;
            var functionDetected = false;
            var cacheStarted = false;

            // Define default function tokens if none are provided
            functionTokens ??= new[] { "Action:", "```\nAction:", "```tool_call\nAction:", "```tool_code\nAction:", "```python\nAction:" };
            var maxFunctionTokenLength = functionTokens?.Max(ft => ft.Length) ?? 0;

            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    if (!setValue)
                    {
                        MostRecentApiResult = res;
                        setValue = true;
                    }

                    string deltaContent = delta.Content;
                    if (res.Choices.FirstOrDefault()?.FinishReason == "function_call")
                    {
                        functionDetected = true;
                    }
                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        // Check if caching should start (if we detect a newline in deltaContent)
                        if (deltaContent.Contains("\n") || deltaContent == "```" || deltaContent == "```\n" || deltaContent == "Action")
                        {
                            cacheStarted = true;
                        }

                        buffer_msg += deltaContent;

                        if (cacheStarted)
                        {
                            // Start checking for function tokens only if caching has started
                            foreach (var functionToken in functionTokens)
                            {
                                if (buffer_msg.Contains(functionToken) && !functionDetected)
                                {
                                    // Split the buffer around the functionToken
                                    var parts = buffer_msg.Split(new[] { functionToken }, 2, StringSplitOptions.None);

                                    // Output the part before the functionToken if it exists
                                    if (!string.IsNullOrEmpty(parts[0]) && parts[0] != "```\n")
                                    {
                                        yield return parts[0];
                                    }

                                    // Mark functionToken detected and update the buffer with the remaining part after the functionToken
                                    functionDetected = true;
                                    buffer_msg = parts.Length > 1 ? parts[1] : string.Empty;

                                    break; // Exit the loop once a function token is detected
                                }
                            }

                            // If buffer_msg exceeds the length of the longest functionToken and no functionToken is detected
                            if (!functionDetected && buffer_msg.Length > maxFunctionTokenLength)
                            {
                                // Wait for the next newline to push the buffer
                                if (deltaContent.Contains("\n"))
                                {
                                    yield return buffer_msg;
                                    buffer_msg = string.Empty;
                                    cacheStarted = false;

                                }
                            }
                        }
                        else
                        {
                            // Output content directly if caching hasn't started
                            yield return buffer_msg;
                            buffer_msg = string.Empty;
                        }

                        // Handle function call accumulation
                        if (functionDetected && delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
                        {
                            if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

                            MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
                            MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
                        }

                        if (functionDetected && res.Choices.FirstOrDefault()?.FinishReason != null)
                        {
                            MostRecentApiResult.Choices.FirstOrDefault().FinishReason = "function_call";
                        }
                    }
                }
            }

            // Handle any remaining content after processing the stream
            if (!functionDetected && buffer_msg.Length > 0)
            {
                yield return buffer_msg;
            }
        }




        //     public async IAsyncEnumerable<string> StreamResponseEnumerableFromLLamaChatbotAsync(string functionToken = "```\n")
        //     {
        //         var req = new LLamaChatRequest(LLamaRequestParameters);
        //         req.Messages = _Messages.ToList();

        //         ChatMessageRole responseRole = null;
        //         bool setValue = false;
        //         MostRecentApiResult = null;
        //var buffer_msg = string.Empty;
        //         await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
        //         {
        //             if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
        //             {
        //                 if (delta.Role != null)
        //                     responseRole = delta.Role;

        //                 string deltaContent = delta.Content;
        //                 if (buffer_msg.Length < functionToken.Length)
        //                     buffer_msg += string.IsNullOrEmpty(deltaContent) ? "" : deltaContent;
        //                 if (!string.IsNullOrEmpty(deltaContent) && !buffer_msg.StartsWith("``") && !buffer_msg.StartsWith("```\n") && (!buffer_msg.StartsWith("```\nAction") || !buffer_msg.StartsWith("```tool_code\nAction")) && !buffer_msg.StartsWith(functionToken))
        //                 {
        //                      yield return deltaContent;
        //                 }
        //                 else
        //                 {
        //                     if (!setValue)
        //                     {
        //                         MostRecentApiResult = res;
        //                         setValue = true;
        //                     }
        //                     else
        //                     {
        //                         if (delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
        //                         {
        //                             if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
        //                                 MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

        //                             MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
        //                             MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
        //                         }

        //                         if (res.Choices.FirstOrDefault()?.FinishReason != null)
        //                         {
        //                             MostRecentApiResult.Choices.FirstOrDefault().FinishReason = res.Choices.FirstOrDefault()?.FinishReason;
        //                         }

        //                     }

        //                 }

        //             }
        //         }
        //     }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromPhi3ChatbotAsync()
        {
            var req = new LLamaChatRequest(LLamaRequestParameters);
            req.Messages = _Messages.ToList();

            StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            var buffer_msg = string.Empty;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    string deltaContent = delta.Content;
                    buffer_msg += string.IsNullOrEmpty(deltaContent) ? "" : deltaContent;
                    //llama 3 70b 微调后，遇到function call 时，会先输出"```\n"
                    if (!string.IsNullOrEmpty(deltaContent) && !buffer_msg.StartsWith("Action") && !buffer_msg.StartsWith(" Action"))
                    {
                        responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                    else
                    {
                        if (!setValue)
                        {
                            MostRecentApiResult = res;
                            setValue = true;
                        }
                        else
                        {
                            if (delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
                            {
                                if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                                    MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
                            }

                            if (res.Choices.FirstOrDefault()?.FinishReason != null)
                            {
                                MostRecentApiResult.Choices.FirstOrDefault().FinishReason = res.Choices.FirstOrDefault()?.FinishReason;
                            }

                        }

                    }

                }
            }

            if (responseRole != null && responseStringBuilder.Length > 0)
            {
                AppendMessage(responseRole, responseStringBuilder.ToString());
            }
        }

        public async IAsyncEnumerable<string> StreamResponseEnumerableFromGLM4ChatbotAsync()
        {
            var req = new LLamaChatRequest(LLamaRequestParameters);
            req.Messages = _Messages.ToList();

            StringBuilder responseStringBuilder = new StringBuilder();
            ChatMessageRole responseRole = null;
            bool setValue = false;
            MostRecentApiResult = null;
            var buffer_msg = string.Empty;
            await foreach (var res in _endpoint.StreamChatEnumerableAsync(req))
            {
                if (res.Choices.FirstOrDefault()?.Delta is ChatMessage delta)
                {
                    if (delta.Role != null)
                        responseRole = delta.Role;

                    string deltaContent = delta.Content;
                    buffer_msg += string.IsNullOrEmpty(deltaContent) ? "" : deltaContent;
                    //llama 3 70b 微调后，遇到function call 时，会先输出"```\n"
                    if (!string.IsNullOrEmpty(deltaContent) && !buffer_msg.StartsWith("Action") && !buffer_msg.StartsWith(" Action"))
                    {
                        responseStringBuilder.Append(deltaContent);
                        yield return deltaContent;
                    }
                    else
                    {
                        if (!setValue)
                        {
                            MostRecentApiResult = res;
                            setValue = true;
                        }
                        else
                        {
                            if (delta.FunctionCall != null && !string.IsNullOrEmpty(delta.FunctionCall.Arguments))
                            {
                                if (MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall == null)
                                    MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall = new FunctionCall();

                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Arguments += delta.FunctionCall.Arguments;
                                MostRecentApiResult.Choices.FirstOrDefault().Delta.FunctionCall.Name += delta.FunctionCall.Name;
                            }

                            if (res.Choices.FirstOrDefault()?.FinishReason != null)
                            {
                                MostRecentApiResult.Choices.FirstOrDefault().FinishReason = res.Choices.FirstOrDefault()?.FinishReason;
                            }

                        }

                    }

                }
            }

            if (responseRole != null && responseStringBuilder.Length > 0)
            {
                AppendMessage(responseRole, responseStringBuilder.ToString());
            }
        }
        #endregion
    }
}
