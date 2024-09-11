using NUnit.Framework;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using OpenAI_API.Moderation;
using OpenAI_API.ChatFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Net.Mime;
using Newtonsoft.Json;

namespace OpenAI_Tests
{
	public class ChatEndpointTests
	{
		[SetUp]
		public void Setup()
		{
			OpenAI_API.APIAuthentication.Default = new OpenAI_API.APIAuthentication(Environment.GetEnvironmentVariable("TEST_OPENAI_SECRET_KEY"));
		}

		[Test]
		public void BasicCompletion()
		{
			var api = new OpenAI_API.OpenAIAPI();

			Assert.IsNotNull(api.Chat);

			var results = api.Chat.CreateChatCompletionAsync(new ChatRequest()
			{
				Model = Model.ChatGPTTurbo,
				Temperature = 0.1,
				MaxTokens = 5,
				Messages = new ChatMessage[] {
					new ChatMessage(ChatMessageRole.User, "Hello!")
				}
			}).Result;
			Assert.IsNotNull(results);
			if (results.CreatedUnixTime.HasValue)
			{
				Assert.NotZero(results.CreatedUnixTime.Value);
				Assert.NotNull(results.Created);
				Assert.Greater(results.Created.Value, new DateTime(2018, 1, 1));
				Assert.Less(results.Created.Value, DateTime.Now.AddDays(1));
			}
			else
			{
				Assert.Null(results.Created);
			}
			Assert.NotNull(results.Object);
			Assert.NotNull(results.Choices);
			Assert.NotZero(results.Choices.Count);
			Assert.AreEqual(ChatMessageRole.Assistant, results.Choices[0].Message.Role);
			Assert.That(results.Choices.All(c => c.Message.Role.Equals(ChatMessageRole.Assistant)));
			Assert.That(results.Choices.All(c => c.Message.Content.Length > 1));
		}
		[Test]
		public void BasicCompletionWithNames()
		{
			var api = new OpenAI_API.OpenAIAPI();

			Assert.IsNotNull(api.Chat);

			var results = api.Chat.CreateChatCompletionAsync(new ChatRequest()
			{
				Model = Model.ChatGPTTurbo,
				Temperature = 0.1,
				MaxTokens = 5,
				Messages = new ChatMessage[] {
					new ChatMessage(ChatMessageRole.System, "You are the moderator in this workplace chat.  Answer any questions asked of the participants."),
					new ChatMessage(ChatMessageRole.User, "Hello everyone") { Name="John"},
					new ChatMessage(ChatMessageRole.User, "Good morning all")  { Name="Edward"},
					new ChatMessage(ChatMessageRole.User, "Is John here?  Answer yes or no.") { Name = "Cindy" }
					}
			}).Result;
			Assert.IsNotNull(results);
			if (results.CreatedUnixTime.HasValue)
			{
				Assert.NotZero(results.CreatedUnixTime.Value);
				Assert.NotNull(results.Created);
				Assert.Greater(results.Created.Value, new DateTime(2018, 1, 1));
				Assert.Less(results.Created.Value, DateTime.Now.AddDays(1));
			}
			else
			{
				Assert.Null(results.Created);
			}
			Assert.NotNull(results.Object);
			Assert.NotNull(results.Choices);
			Assert.NotZero(results.Choices.Count);
			Assert.AreEqual(ChatMessageRole.Assistant, results.Choices[0].Message.Role);
			Assert.That(results.Choices.All(c => c.Message.Role.Equals(ChatMessageRole.Assistant)));
			Assert.That(results.Choices.All(c => c.Message.Content.Length > 1));
			Assert.That(results.ToString().ToLower().Contains("yes"));
		}
		[Test]
		public void SimpleCompletion()
		{
			var api = new OpenAI_API.OpenAIAPI();

			Assert.IsNotNull(api.Chat);

			var results = api.Chat.CreateChatCompletionAsync("Hello!").Result;
			Assert.IsNotNull(results);
			if (results.CreatedUnixTime.HasValue)
			{
				Assert.NotZero(results.CreatedUnixTime.Value);
				Assert.NotNull(results.Created);
				Assert.Greater(results.Created.Value, new DateTime(2018, 1, 1));
				Assert.Less(results.Created.Value, DateTime.Now.AddDays(1));
			}
			else
			{
				Assert.Null(results.Created);
			}
			Assert.NotNull(results.Object);
			Assert.NotNull(results.Choices);
			Assert.NotZero(results.Choices.Count);
			Assert.AreEqual(ChatMessageRole.Assistant, results.Choices[0].Message.Role);
			Assert.That(results.Choices.All(c => c.Message.Role.Equals(ChatMessageRole.Assistant)));
			Assert.That(results.Choices.All(c => c.Message.Role == ChatMessageRole.Assistant));
			Assert.That(results.Choices.All(c => c.Message.Content.Length > 1));
			Assert.IsNotEmpty(results.ToString());
		}

		[TestCase("gpt-3.5-turbo")]
		[TestCase("gpt-4")]
		public void ChatBackAndForth(string model)
		{
			var api = new OpenAI_API.OpenAIAPI();

			var chat = api.Chat.CreateConversation(new ChatRequest());
			chat.Model = model;
			chat.RequestParameters.Temperature = 0;

			chat.AppendSystemMessage("You are a teacher who helps children understand if things are animals or not.  If the user tells you an animal, you say \"yes\".  If the user tells you something that is not an animal, you say \"no\".  You only ever respond with \"yes\" or \"no\".  You do not say anything else.");
			chat.AppendUserInput("Is this an animal? Cat");
			chat.AppendExampleChatbotOutput("Yes");
			chat.AppendUserInput("Is this an animal? House");
			chat.AppendExampleChatbotOutput("No");
			chat.AppendUserInput("Is this an animal? Dog");
			string res = chat.GetResponseFromChatbotAsync().Result;
			Assert.NotNull(res);
			Assert.IsNotEmpty(res);
			Assert.AreEqual("Yes", res.Trim());
			chat.AppendUserInput("Is this an animal? Chair");
			res = chat.GetResponseFromChatbotAsync().Result;
			Assert.NotNull(res);
			Assert.IsNotEmpty(res);
			Assert.AreEqual("No", res.Trim());
		}
		[Test]
		public async Task SummarizeFunctionResult()
		{
			try
			{
                var api = new OpenAI_API.OpenAIAPI();
                var functionList = new List<Function>
                {
                    BuildFunctionForTest()
                };
                var conversation = api.Chat.CreateConversation(new ChatRequest { 
                    Model = Model.ChatGPTTurbo0613,
                    Functions = functionList,
		    Temperature = 0
                });
                conversation.AppendUserInput("What is the weather like in Boston?");

                var response = await conversation.GetResponseFromChatbotAsync();

                Assert.IsNull(response);

                var functionMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = "get_current_weather",
                    Content = "{\"temperature\": \"22\", \"unit\": \"celsius\", \"description\": \"sunny\"}"
                };
                conversation.AppendMessage(functionMessage);
                response = await conversation.GetResponseFromChatbotAsync();

				Assert.AreEqual("The current weather in Boston is sunny with a temperature of 22 degrees Celsius.", response);

			}
			catch(NullReferenceException ex)
			{
				Console.WriteLine(ex.Message, ex.StackTrace);
				Assert.False(true);
			}
        }

        [Test]
        public async Task SummarizeGemmaFunctionResult()
        {
            try
            {
                var api = new OpenAI_API.OpenAIAPI("0");
                api.ApiUrlFormat = "https://gemma2.nngeo.net/v1/{1}";
                var functionList = new List<OpenAIFunction>
                {
                    BuilGemmaFunctionForTest()
                };
                var conversation = api.Chat.CreateConversation(new GemmaChatRequest
                {
                    Model = "Gemma",
                    Functions = functionList,
                    Temperature = 1
                });
                conversation.AppendUserInput("What is the weather like in Boston?");

                var response = await conversation.GetResponseFromGemmaChatbotAsync();

                //Assert.IsNull(response);
                //如果使用function，返回的结果中会包含function_call
                if(conversation.MostRecentApiResult.Choices.Count > 0)
                {
                    Assert.NotNull(conversation.MostRecentApiResult.Choices.FirstOrDefault().FinishReason == "function_call");
                    Assert.NotNull(conversation.MostRecentApiResult.Choices.FirstOrDefault().Message.FunctionCall);
                    Assert.NotNull(conversation.MostRecentApiResult.Choices.FirstOrDefault().Message.FunctionCall.Name, "get_current_weather");
                    Assert.NotNull(conversation.MostRecentApiResult.Choices.FirstOrDefault().Message.FunctionCall.Arguments);
                }
                var funcMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = "get_current_weather",
                    //需要调用的function的参数
                    Content = JsonConvert.SerializeObject(new { name = conversation.MostRecentApiResult.Choices.FirstOrDefault().Message.FunctionCall.Name, argument = conversation.MostRecentApiResult.Choices.FirstOrDefault().Message.FunctionCall.Arguments })
                };
                conversation.AppendMessage(funcMessage);


                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Name = "get_current_weather",
                    Content = "{\"temperature\": \"22\", \"unit\": \"celsius\", \"description\": \"sunny\"}"
                };
                conversation.AppendMessage(toolMessage);
                response = await conversation.GetResponseFromGemmaChatbotAsync();

                Assert.AreEqual("The current weather in Boston is sunny with a temperature of 22 degrees Celsius.", response);

            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                Assert.False(true);
            }
        }

        [Test]
        public async Task SummarizeFunctionStreamResult()
        {
            try
            {
                var api = new OpenAI_API.OpenAIAPI("0");
				api.ApiUrlFormat = "http://localhost:8000/v1/{1}";
                var functionList = new List<Function>
                {
                    BuildFunctionForTest()
                };
                var conversation = api.Chat.CreateConversation(new ChatRequest
                {
                    Model = Model.ChatGPTTurbo0613,
                    Functions = functionList,
                    Temperature = 0
                });
                conversation.AppendUserInput("What is the weather like in Boston?");
				string response = string.Empty;

                await foreach (var res in conversation.StreamResponseEnumerableFromChatbotAsync())
                {
                    response += res;
                }

                Assert.IsTrue(string.IsNullOrEmpty(response));
				string param = "{\n  \"location\": \"Boston, MA\"\n}";
				Assert.NotNull(conversation.MostRecentApiResult.Choices[0]);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall);
                Assert.IsTrue(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments == param);
                var functionMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = "get_current_weather",
                    Content = "{\"temperature\": \"22\", \"unit\": \"celsius\", \"description\": \"sunny\"}"
                };
                conversation.AppendMessage(functionMessage);
                response = await conversation.GetResponseFromChatbotAsync();

                Assert.AreEqual("The current weather in Boston is sunny with a temperature of 22 degrees Celsius.", response);

            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                Assert.False(true);
            }
        }

		[Test]
        public async Task SummarizeGemmaFunctionStreamResult()
        {
            try
            {
                var api = new OpenAI_API.OpenAIAPI("0");
                api.ApiUrlFormat = "http://localhost:8000/v1/{1}";
                var functionList = new List<OpenAIFunction>
                {
                    BuilGemmaFunctionForTest()
                };
                var gemmarequest = new GemmaChatRequest
                {
                    Model = "gemma2",
                    Functions = functionList,
                    Temperature = 1
                };
                var conversation = api.Chat.CreateConversation(gemmarequest);
                conversation.AppendUserInput("告诉我波士顿今天的气温多少度，华氏");
                string response = string.Empty;

                await foreach (var res in conversation.StreamResponseEnumerableFromGemmaChatbotAsync())
                {
                    response += res;
                }

                //Assert.IsTrue(string.IsNullOrEmpty(response));
                string param = "{\n  \"location\": \"Boston\"\n}";
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0]);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall);
                //Assert.IsTrue(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments == param);
                var funcMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = "get_current_weather",
                    Content = JsonConvert.SerializeObject(new { name = "get_current_weather", argument = param })
                };
                conversation.AppendMessage(funcMessage);
                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Name = "get_current_weather:",
                    Content = "{\"temperature\": \"22\", \"unit\": \"celsius\", \"description\": \"sunny\"}"
                };
                conversation.AppendMessage(toolMessage);
                //response = await conversation.GetResponseFromChatbotAsync();
                await foreach (var res in conversation.StreamResponseEnumerableFromGemmaChatbotAsync())
                {
                    response += res;
                }

                Assert.AreEqual("在波士顿，今天气温是22华氏度，天气晴朗。", response);

            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                Assert.False(true);
            }
        }

        [Test]
        public async Task SummarizeLLamaFunctionStreamResultNew()
        {
            try
            {
                var api = new OpenAI_API.OpenAIAPI("0");
                api.ApiUrlFormat = "https://gemma.nngeo.net/v1/{1}";
                var functionList = new List<OpenAIFunction>
                {
                    //BuildImageFunction(),
                    BuildPythonFunction()
                };
                var llamarequest = new LLamaChatRequest
                {
                    Model = Model.ChatGPTTurbo0613,
                    Functions = functionList,
                    Temperature = 1
                };
                var conversation = api.Chat.CreateConversation(llamarequest);
                conversation.AppendMessage(new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = "### 你是一个智能助手，可以回答用户提出的各种问题.\n\n ### 使用markdown格式展示回复内容 \n\n ### 如果是用户请求的是图片，那么回复中首先使用 markdown 格式展示图片，然后连续两个换行符后回复其他内容"
                });
                //conversation.AppendUserInput("画一张图，内容是：可爱的小猫在喝水");
                conversation.AppendUserInput("帮我生成一个 hello.txt 文件，文件中打印1行 hello world");
                string response = string.Empty;

                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }

                //Assert.IsTrue(string.IsNullOrEmpty(response));
               
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0]);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments);
                string param = "{ \"code\": \"import io\\nimport base64\\n\\nbuffer = io.BytesIO()\\nfor i in range(3):\\n    buffer.write(b'hello world\\n')\\n\\nbuffer.seek(0)  # 回到缓冲区的开头\\ndata = buffer.read()\\nresult = 'data:application/octet-stream;base64,' + base64.b64encode(data).decode('utf-8')\\nbuffer.close()\"}";
                var funcMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = "python",
                    Content = JsonConvert.SerializeObject(new { name = conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Name, arguments = param })
                };
                conversation.AppendMessage(funcMessage);
                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Name = "python",
                    Content = "{\"Result\":\"Syntax error during AST parsing: unexpected character after line continuation character (<unknown>, line 1)\",\"Descriptions\":\"代码出现错误，请修正代码后重试。.\"}"
                };
                conversation.AppendMessage(toolMessage);
                //response = await conversation.GetResponseFromChatbotAsync();
                response = string.Empty;
                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }
                
                Assert.NotNull(response);
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                Assert.False(true);
            }
        }

        [Test]
        public async Task SummarizeLLamaMultiTurnFunctionStreamResultNew()
        {
            try
            {
                var api = new OpenAI_API.OpenAIAPI("0");
                api.ApiUrlFormat = "https://gemma.nngeo.net/v1/{1}";
                //api.ApiUrlFormat = "http://localhost:8000/v1/{1}";
                var functionList = new List<OpenAIFunction>
                {
                    //BuildImageFunction(),
                    BuildPythonFunction(),
                    BuildUrlTypeFunction(),
                    BuildImageContentFunction()
                };
                var llamarequest = new LLamaChatRequest
                {
                    Model = Model.ChatGPTTurbo0613,
                    Functions = functionList,
                    Temperature = 0.7,
                    TopP = 0.9
                };
                var conversation = api.Chat.CreateConversation(llamarequest);
                conversation.AppendMessage(new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = "### 你是一个智能助手，可以回答用户提出的各种问题.\n\n ### 使用markdown格式展示回复内容 \n\n ### 如果是用户请求的是图片，那么回复中首先使用 markdown 格式展示图片，然后连续两个换行符后回复其他内容"
                });
                //conversation.AppendUserInput("画一张图，内容是：可爱的小猫在喝水");
                conversation.AppendUserInput("http://192.168.50.96:5261/api/attachfile/file?id=01J7ES9JPF0DAPJRX84DXC6G4H");
                string response = string.Empty;

                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }

                //Assert.IsTrue(string.IsNullOrEmpty(response));

                Assert.NotNull(conversation.MostRecentApiResult.Choices[0]);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments);
                
                var funcMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Name,
                    Content = JsonConvert.SerializeObject(new { name = conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Name, arguments = conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments })
                };
                conversation.AppendMessage(funcMessage);
                string param = "{ \"result\": \"这是一个图片 url ，可以使用 readimage 来读取内容\"}";
                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Name = "checkurl",
                    Content = param
                };
                conversation.AppendMessage(toolMessage);
                //response = await conversation.GetResponseFromChatbotAsync();
                response = string.Empty;
                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }

                Assert.IsNotNull(conversation.MostRecentApiResult.Choices[0].FinishReason);
                Assert.AreEqual("function_call", conversation.MostRecentApiResult.Choices[0].FinishReason);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments);
                Assert.AreEqual("readimage", conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Name);

            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                Assert.False(true);
            }
        }

        [Test]
        public async Task SummarizeLLamaFunctionStreamResult()
        {
            try
            {
                var api = new OpenAI_API.OpenAIAPI("0");
                api.ApiUrlFormat = "http://localhost:8000/v1/{1}";
                var functionList = new List<OpenAIFunction>
                {
                    BuildImageFunction(),
                    BuildPythonFunction()
                };
				var llamarequest = new LLamaChatRequest
				{
					Model = Model.ChatGPTTurbo0613,
					Functions = functionList,
					Temperature = 1
				};
                var conversation = api.Chat.CreateConversation(llamarequest);
                conversation.AppendMessage(new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = "### 你是一个智能助手，可以回答用户提出的各种问题.\n\n ### 使用markdown格式展示回复内容 \n\n ### 如果是用户请求的是图片，那么回复中首先使用 markdown 格式展示图片，然后连续两个换行符后回复其他内容"
                });
                //conversation.AppendUserInput("画一张图，内容是：可爱的小猫在喝水");
                conversation.AppendUserInput("A商品1月销售200个，2月销售180个，3月销售320个，B商品1月销售20个，2月销售57,3月销售40个，帮我生成统计图表");
                string response = string.Empty;

                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }

                //Assert.IsTrue(string.IsNullOrEmpty(response));
                string param = "{\"Id\":\"01J5WGV9MFZ6Z9HKX4JARW0KYT\",\"Url\":\"https://chatapi.nngeo.net/api/image/66c6ed041f7689dd06ebe532\",\"Descriptions\":\"图片已生成并保存到服务器\"}";
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0]);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall);
                Assert.NotNull(conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments);
                var funcMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Function,
                    Name = "drawimage",
                    Content = JsonConvert.SerializeObject(new { name= conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Name, arguments = conversation.MostRecentApiResult.Choices[0].Delta.FunctionCall.Arguments })
                };
                conversation.AppendMessage(funcMessage);
                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Name = "drawimage:",
                    Content = "{\"Id\":\"01J5WGV9MFZ6Z9HKX4JARW0KYT\",\"Url\":\"https://chatapi.nngeo.net/api/image/66c6ed041f7689dd06ebe532\",\"Descriptions\":\"使用 markdown 格式显示图片\"}"
                };
                conversation.AppendMessage(toolMessage);
                //response = await conversation.GetResponseFromChatbotAsync();
                response = string.Empty;
                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }
                conversation.AppendMessage(new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = response
                });
                Assert.NotNull(response);

                conversation.AppendUserInput("使用 markdown 将图片展示出来");
                response = string.Empty;
                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }
                Assert.NotNull(response);
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message, ex.StackTrace);
                Assert.False(true);
            }
        }

        [Test]
		public void ChatWithNames()
		{
			var api = new OpenAI_API.OpenAIAPI();

			var chat = api.Chat.CreateConversation(new ChatRequest());
			chat.RequestParameters.Temperature = 0;

			chat.AppendSystemMessage("You are the moderator in this workplace chat.  Answer any questions asked of the participants.");
			chat.AppendUserInputWithName("John", "Hello everyone");
			chat.AppendUserInputWithName("Edward", "Good morning all");
			chat.AppendUserInputWithName("Cindy", "Is John here?  Answer yes or no.");
			chat.AppendExampleChatbotOutput("Yes");
			chat.AppendUserInputWithName("Cindy", "Is Monica here?  Answer yes or no.");
			string res = chat.GetResponseFromChatbotAsync().Result;
			Assert.NotNull(res);
			Assert.IsNotEmpty(res);
			Assert.That(res.ToLower().Contains("no"));
			chat.AppendUserInputWithName("Cindy", "Is Edward here?  Answer yes or no.");
			res = chat.GetResponseFromChatbotAsync().Result;
			Assert.NotNull(res);
			Assert.IsNotEmpty(res);
			Assert.That(res.ToLower().Contains("yes"));
		}


		[Test]
		public async Task StreamCompletionEnumerableAsync_ShouldStreamData()
		{
			var api = new OpenAI_API.OpenAIAPI();
			Assert.IsNotNull(api.Chat);

			var req = new ChatRequest()
			{
				Model = Model.ChatGPTTurbo,
				Temperature = 0.2,
				MaxTokens = 500,
				Messages = new ChatMessage[] {
					new ChatMessage(ChatMessageRole.User, "Please explain how mountains are formed in great detail.")
				}
			};

			var chatResults = new List<ChatResult>();
			await foreach (var res in api.Chat.StreamChatEnumerableAsync(req))
			{
				chatResults.Add(res);
			}

			Assert.Greater(chatResults.Count, 100);
			Assert.That(chatResults.Select(cr => cr.Choices[0].Delta.Content).Count(c => !string.IsNullOrEmpty(c)) > 50);
		}

		[Test]
		public async Task StreamingConversation()
		{
			var api = new OpenAI_API.OpenAIAPI();

			var chat = api.Chat.CreateConversation(new ChatRequest());
			chat.RequestParameters.MaxTokens = 500;
			chat.RequestParameters.Temperature = 0.2;
			chat.Model = Model.ChatGPTTurbo;

			chat.AppendSystemMessage("You are a helpful assistant who is really good at explaining things to students.");
			chat.AppendUserInput("Please explain to me how mountains are formed in great detail.");

			string result = "";
			int streamParts = 0;

			await foreach (var streamResultPart in chat.StreamResponseEnumerableFromChatbotAsync())
			{
				result += streamResultPart;
				streamParts++;
			}

			Assert.NotNull(result);
			Assert.IsNotEmpty(result);
			Assert.That(result.ToLower().Contains("mountains"));
			Assert.Greater(result.Length, 200);
			Assert.Greater(streamParts, 5);

			Assert.AreEqual(ChatMessageRole.User, chat.Messages.Last().Role);
			Assert.AreEqual(result, chat.Messages.Last().Content);
		}
		public static Function BuildFunctionForTest()
		{
            var parameters = new JObject
            {
                ["type"] = "function",
                ["required"] = new JArray("location"),
                ["properties"] = new JObject
                {
                    ["location"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "The city and state, e.g. San Francisco, CA"
                    },
                    ["unit"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("celsius", "fahrenheit")
                    }
                }
            };

			var functionName = "get_current_weather";
			var functionDescription = "Gets the current weather in a given location";

			return new Function(functionName, functionDescription, parameters);
        }

        public static LLamaFunction BuildLLamaFunctionForTest()
        {
            var parameters = new JObject
            {
                ["type"] = "function",
                ["required"] = new JArray("location"),
                ["properties"] = new JObject
                {
                    ["location"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "The city and state, e.g. San Francisco, CA"
                    },
                    ["unit"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("celsius", "fahrenheit")
                    }
                }
            };

            var functionName = "get_current_weather";
            var functionDescription = "Gets the current weather in a given location";

            var func = new Function(functionName, functionDescription, parameters);
			return new LLamaFunction
			{
				Function = func,
				Type = "function"
			};
        }

        public static OpenAIFunction BuildImageFunction()
        {
            var parameters = new JObject
            {
                ["type"] = "object",
                ["required"] = new JArray("prompt"),
                ["properties"] = new JObject
                {
                    ["prompt"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "生成图片的英文提示语"
                    },
                    ["description"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "生成图片的中文提示语"
                    },
                }
            };

            var functionName = "drawimage";
            var functionDescription = "生成图片，并返回图片url 地址。提示词必须为英文，参考 midjourney 的提示词风格。";

            var function = new Function(functionName, functionDescription, parameters);
            return new OpenAIFunction
            {
                Type = "function",
                Function = function
            };
        }

        public class imageContentParam
        {
            public string url { get; set; }
        }
        public OpenAIFunction BuildImageContentFunction()
        {
            var parameters = new JObject
            {
                ["type"] = "object",
                ["required"] = new JArray("url"),
                ["properties"] = new JObject
                {
                    ["url"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "图片的url地址"
                    }
                }
            };

            var functionName = "readimage";
            var functionDescription = "获取图片的内容，返回图片的文字内容。";

            var function = new Function(functionName, functionDescription, parameters);
            return new OpenAIFunction
            {
                Type = "function",
                Function = function
            };
        }

        public class urlTypeParam
        {
            public string url { get; set; }
        }

        public OpenAIFunction BuildUrlTypeFunction()
        {
            var parameters = new JObject
            {
                ["type"] = "object",
                ["required"] = new JArray("url"),
                ["properties"] = new JObject
                {
                    ["url"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "需要检查的url地址"
                    }
                }
            };

            var functionName = "checkurl";
            var functionDescription = "检查url地址的内容类型，返回内容类型。";

            var function = new Function(functionName, functionDescription, parameters);
            return new OpenAIFunction
            {
                Type = "function",
                Function = function
            };
        }

        public OpenAIFunction BuildPythonFunction()
        {
            var parameters = new JObject
            {
                ["type"] = "object",
                ["required"] = new JArray("code"),
                ["properties"] = new JObject
                {
                    ["code"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "需要执行的Python代码，将需要返回的内容保存到变量result"
                    }
                }
            };

            var functionName = "python";
            var functionDescription = @"
# 执行Python代码，用于简单计算、绘制基本的图表或者流程图。如果需要返回结果，需要将结果保存在 result 变量中，如果没有返回结果，则默认返回标准打印输出
## matplotlib 使用非交互式的后端，严禁在代码中使用 plt.show()或者 Image.show() 函数，否则会导致程序无法返回结果，应该使用 plt.savefig() 保存图像到BytesIO对象。
## 最后结果必须保存在 result 变量中，否则无法返回结果。
## 绘制统计图表示例代码：
```python
import matplotlib.pyplot as plt
import io
import base64
months = ['January', 'February', 'March']
sales_A = [200, 180, 320]
sales_B = [20, 57, 40]
plt.figure(figsize=(10, 6))
plt.bar(months, sales_A, color='green', width=0.4, label='Product A')
plt.bar(months, sales_B, color='blue', width=0.4, label='Product B', alpha=0.7)
plt.xlabel('Month')
plt.ylabel('Sales')
plt.title('Sales of Products A and B (in units)')
buf = io.BytesIO()
plt.savefig(buf, format='png')
buf.seek(0)
img_base64 = base64.b64encode(buf.getvalue()).decode('utf-8')
result = 'data:image/png;base64,' + img_base64
buf.close()
plt.close()
```
### 绘制流程图示例代码：
```python
from diagrams import Diagram
from diagrams.aws.compute import EC2
from diagrams.aws.network import ELB
from diagrams.aws.database import RDS
from PIL import Image

with Diagram(""流程图"", show=False, outformat=""png"", filename=""temp""):
    apply = EC2(""申请"")
    audit = ELB(""审核"")
    registration = RDS(""回岗登记"")

    apply >> audit >> registration
result = Image.open(""temp.png"")
``
### 生成文件：
```python
import io
import base64

buffer = io.BytesIO()
for i in range(3):
    buffer.write(b'hello world\n')

buffer.seek(0)  # 回到缓冲区的开头
data = buffer.read()
result = 'data:application/octet-stream;base64,' + base64.b64encode(data).decode('utf-8')
buffer.close()
```

## 注意事项：不要在代码中添加注释
### 环境中包含以下库：
- numpy
- pandas
- matplotlib
- PIL
- requests
- json
- datetime
- random
- math
- os
- sys
- opencv-python
- qrcode
- diagrams
";


            var function = new Function(functionName, functionDescription, parameters);
            return new OpenAIFunction
            {
                Type = "function",
                Function = function
            };
        }

        public static OpenAIFunction BuilGemmaFunctionForTest()
        {
            var parameters = new JObject
            {
                ["type"] = "function",
                ["required"] = new JArray("location"),
                ["properties"] = new JObject
                {
                    ["location"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "The city and state, e.g. San Francisco, CA"
                    },
                    ["unit"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("celsius", "fahrenheit")
                    }
                }
            };

            var functionName = "get_current_weather";
            var functionDescription = "Gets the current weather in a given location";

            var func = new Function(functionName, functionDescription, parameters);
            return new GemmaFunction
            {
                Function = func,
                Type = "function"
            };
        }
    }
}
