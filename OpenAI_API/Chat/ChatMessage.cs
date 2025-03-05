﻿using Newtonsoft.Json;
using OpenAI_API.ChatFunctions;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenAI_API.Chat
{
	/// <summary>
	/// Chat message sent or received from the API. Includes who is speaking in the "role" and the message text in the "content"
	/// </summary>
	public class ChatMessage
	{
		/// <summary>
		/// Creates an empty <see cref="ChatMessage"/>, with <see cref="Role"/> defaulting to <see cref="ChatMessageRole.User"/>
		/// </summary>
		public ChatMessage()
		{
			this.Role = ChatMessageRole.User;
		}

		/// <summary>
		/// Constructor for a new Chat Message
		/// </summary>
		/// <param name="role">The role of the message, which can be "system", "assistant" or "user"</param>
		/// <param name="content">The text to send in the message</param>
		public ChatMessage(ChatMessageRole role, string content)
		{
			this.Role = role;
			this.Content = content;
		}

		[JsonProperty("role")]
		internal string rawRole { get; set; }

		/// <summary>
		/// The role of the message, which can be "system", "assistant, "user", or "function".
		/// </summary>
		[JsonIgnore]
		public ChatMessageRole Role
		{
			get
			{
				return ChatMessageRole.FromString(rawRole);
			}
			set
			{
				rawRole = value.ToString();
			}
		}

        /// <summary>
        /// The content of the message
        /// </summary>
        [JsonProperty("content", NullValueHandling = NullValueHandling.Include)]
        public string Content { get; set; }

		/// <summary>
		/// An optional name of the user in a multi-user chat 
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }
        /// <summary>
        /// Optional field function call
        /// The name and arguments of a function that should be called, as generated by the model.
        /// </summary>
        [JsonProperty("function_call")]
        public FunctionCall FunctionCall { get; set; }
		[JsonProperty("reasoning_content")]
		public string ReasoningContent { get; set; }
    }
}
