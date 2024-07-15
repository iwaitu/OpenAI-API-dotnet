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
                api.ApiUrlFormat = "http://localhost:8000/v1/{1}";
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

                Assert.IsNull(response);
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
                api.ApiUrlFormat = "http://gemma2.nngeo.net/v1/{1}";
                var functionList = new List<OpenAIFunction>
                {
                    BuilGemmaFunctionForTest()
                };
                var gemmarequest = new GemmaChatRequest
                {
                    Model = "Gemma",
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

                Assert.AreEqual("在波士顿，今天的温度是22摄氏度，天气晴朗。", response);

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
                    BuildLLamaFunctionForTest()
                };
				var llamarequest = new LLamaChatRequest
				{
					Model = Model.ChatGPTTurbo0613,
					Functions = functionList,
					Temperature = 1
				};
                var conversation = api.Chat.CreateConversation(llamarequest);
                conversation.AppendUserInput("告诉我波士顿今天的气温多少度，华氏");
                string response = string.Empty;

                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
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
                    Content = JsonConvert.SerializeObject(new { name= "get_current_weather", argument= param })
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
                await foreach (var res in conversation.StreamResponseEnumerableFromLLamaChatbotAsync())
                {
                    response += res;
                }

                Assert.AreEqual("The current weather in Boston is sunny with a temperature of 22 degrees Celsius.", response);

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
