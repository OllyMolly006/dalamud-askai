﻿using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json.Linq;

namespace xivgpt
{
    using Dalamud.Plugin;
    using Dalamud.Plugin.Services;

    public class ChatGPTPlugin : IDalamudPlugin
    {
        public string Name =>"Chat with AI for FFXIV";
        private const string commandName = "/gpt";
        private static bool drawConfiguration;
        
        private Configuration configuration;
        private IChatGui chatGui;
        [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] private static ICommandManager CommandManager { get; set; } = null!;

        private string configKey;
        private string configEndpoint;
        private string configModel;

        private int configMaxTokens;
        private bool configLineBreaks;
        private bool configAdditionalInfo;
        private bool configShowPrompt;
        
        public ChatGPTPlugin(IDalamudPluginInterface dalamudPluginInterface, IChatGui chatGui, ICommandManager commandManager)
        {
            this.chatGui = chatGui;

            configuration = (Configuration) dalamudPluginInterface.GetPluginConfig() ?? new Configuration();
            configuration.Initialize(dalamudPluginInterface);

            LoadConfiguration();
            
            dalamudPluginInterface.UiBuilder.Draw += DrawConfiguration;
            dalamudPluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
            
            commandManager.AddHandler(commandName, new CommandInfo(GPTCommand)
            {
                HelpMessage = "/gpt whatever you want to ask ChatGPT/OpenAI's completion API\n/gpt cfg → open the configuration window",
                ShowInHelp = true
            });
        }
        private void GPTCommand(string command, string args)
        {
            if (configKey == string.Empty)
            {
                chatGui.Print("ChatAI>> enter an API key in the configuration");
                OpenConfig();
                return;
            }

            if (configEndpoint == string.Empty)
            {
                chatGui.Print("ChatAI>> enter an Endpoint path in the configuration");
                OpenConfig();
                return;
            }

            if (configModel == string.Empty)
            {
                chatGui.Print("ChatAI>> enter a model name in the configuration");
                OpenConfig();
                return;
            }


            if (args == string.Empty)
            {
                chatGui.Print("ChatAI> enter a prompt after the /gpt command");
                return;
            }

            if (args == "cfg")
            {
                OpenConfig();
                return;
            }

            Task.Run(() => SendPrompt(args));
        }

        private async Task SendPrompt(string input)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configKey);

            const string systemPrompt = "You are interacting through the in-game chat of the MMORPG Final Fantasy XIV, as such your responses can only be displayed as simple text without any markup and as concisely as possible.";

            input = Regex.Replace(input, @"(\\[^\n]|""|')", "");
            var requestBody = "{" +
                              $"\"model\": \"{configModel}\"," +
                              "\"messages\":" +
                              "[" +
                                $"{{\"role\": \"system\",\"content\": \"{systemPrompt}\"}}, " +
                                $"{{\"role\": \"user\", \"content\": \"{input}\"}}" +
                              "]," +
                              $"\"max_tokens\": {configMaxTokens}" +
                              "}";
            
            
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(configEndpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            var responseJson = JObject.Parse(responseBody);
            var text = (string) responseJson.SelectToken("choices[0].message.content");
            
            if (text != null)
            {
                if(configLineBreaks)
                    text = text.Replace("\r", "").Replace("\n", "");
                
                const int chunkSize = 1000;
                var regex = new Regex(@".{1," + chunkSize + @"}(\s+|$)"); //jesus take the wheel
                var chunks = regex.Matches(text).Select(match => match.Value);
                chunks = chunks.ToList();
                
                if(configAdditionalInfo)
                    chatGui.Print($"ChatGPT>>\nprompt: {input}" +
                                  $"\nmodel: {configModel}" +
                                  $"\nmax_tokens: {configMaxTokens}" +
                                  $"\nresponse length: {text.Length}" +
                                  $"\nchunks: {chunks.Count()}");
                
                if(configShowPrompt)
                    chatGui.Print($"ChatGPT Prompt: {input}");
                
                foreach (var chunk in chunks)
                    chatGui.Print($"ChatGPT: {chunk}");
            }
            else
            {
                var errorMessage = "ChatGPT>> Error: text is null";

                if (configAdditionalInfo)
                {
                    errorMessage += $"\nmodel: {configModel}" +
                                    $"\nmax_tokens: {configMaxTokens}" +
                                    $"\nresponse code: {(int) response.StatusCode} - {response.StatusCode}";
                }
                else
                    errorMessage += "\nYou can enable additional info in the configuration. If the issue persists, please report it on github.";

                chatGui.Print(errorMessage);
            }
        }

        #region configuration
        
        private void DrawConfiguration()
        {
            if (!drawConfiguration)
                return;
            
            ImGui.Begin($"{Name} Configuration", ref drawConfiguration);
            
            ImGui.Text("currently used model:");
            ImGui.SameLine();
/*            if (ImGui.SmallButton($"{Configuration.Model}"))
            {
                const string modelsDocs = "https://platform.openai.com/docs/models/gpt-4o";
                Util.OpenLink(modelsDocs);
            }*/
            ImGui.Spacing();
            ImGui.InputText("API key", ref configKey, 100);
            ImGui.SameLine();
/*            if (ImGui.SmallButton("get API key"))
            {
                const string apiKeysUrl = "https://platform.openai.com/account/api-keys";
                Util.OpenLink(apiKeysUrl);
            }*/


            ImGui.Spacing();
            ImGui.InputText("Endpoint name", ref configEndpoint, 100);
            ImGui.SameLine();
/*            if (ImGui.SmallButton("set up Endpoint"))
            {
                const string apiKeysUrl = "https://platform.openai.com/account/api-keys";
                Util.OpenLink(apiKeysUrl);
            }*/

            ImGui.Spacing();
            ImGui.InputText("Model name", ref configModel, 100);
            ImGui.SameLine();
/*            if (ImGui.SmallButton("set up model name"))
            {
                const string apiKeysUrl = "https://platform.openai.com/account/api-keys";
                Util.OpenLink(apiKeysUrl);
            }*/


            ImGui.Spacing();
            ImGui.SliderInt("max_tokens", ref configMaxTokens, 8, 4096);
            ImGui.SameLine();
            if (ImGui.SmallButton("learn more"))
            {
                const string conceptsDocs = "https://platform.openai.com/docs/introduction/key-concepts";
                Util.OpenLink(conceptsDocs);
            }
            ImGui.Separator();
            ImGui.Checkbox("remove line breaks from responses", ref configLineBreaks);
            ImGui.Checkbox("show prompt in chat", ref configShowPrompt);
            ImGui.Checkbox("show additional info", ref configAdditionalInfo);
            if (ImGui.Button("Save and Close"))
            {
                SaveConfiguration();

                drawConfiguration = false;
            }
            
            ImGui.End();
        }
        
        private static void OpenConfig()
        {
            drawConfiguration = true;
        }

        private void LoadConfiguration()
        {
            configKey = configuration.ApiKey;
            configEndpoint = configuration.Endpoint;
            configModel = configuration.Model;

            configMaxTokens = configuration.MaxTokens != 0 ? configuration.MaxTokens : 256;
            configLineBreaks = configuration.RemoveLineBreaks;
            configAdditionalInfo = configuration.ShowAdditionalInfo;
        }

        private void SaveConfiguration()
        {
            configuration.ApiKey = configKey;
            configuration.Endpoint = configEndpoint;
            configuration.Model = configModel;

            configuration.MaxTokens = configMaxTokens;
            configuration.RemoveLineBreaks = configLineBreaks;
            configuration.ShowAdditionalInfo = configAdditionalInfo;
            
            PluginInterface.SavePluginConfig(configuration);
        }
        #endregion
        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawConfiguration;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;

            CommandManager.RemoveHandler(commandName);
        }
        
        
    }
}

