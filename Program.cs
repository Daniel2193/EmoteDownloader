using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace EmoteDownloader
{
    class Program
    {
        private static string version = "1.0.2";
        public static async Task<int> Main(params string[] args)
        {
            RootCommand rootCommand = new RootCommand(
                description: "Bulk download emotes from Twitch/BTTV/FFZ/7TV"
            );
            Option platform = new Option<string>(
                aliases: new string[] { "-p", "--platform" },
                description: "Platform to download from. Valid values: twitch, bttv, ffz, 7tv"
            );
            Option client_id = new Option<string>(
                aliases: new string[] { "--client_id" },
                description: "Client ID, not required if token is provided or platform is not twitch and channel_ids is provided"
            );
            Option client_secret = new Option<string>(
                aliases: new string[] { "--client_secret" },
                description: "Client Secret, not required if token is provided or platform is not twitch and channel_ids is provided"
            );
            Option token = new Option<string>(
                aliases: new string[] { "-t", "--token" },
                description: "Token, not required if client ID and secret are provided or platform is not twitch and channel_ids is provided"
            );
            Option channel_ids = new Option<string>(
                aliases: new string[] {"--channel_ids" },
                description: "Channel IDs, separated by commas. Not required if channel names are provided but"
            );
            Option channel_names = new Option<string>(
                aliases: new string[] {"--channel_names" },
                description: "Channel Names, separated by commas. Not required if channel IDs are provided"
            );
            Option output_dir = new Option<string>(
                defaultValue: Path.Combine(Directory.GetCurrentDirectory(), "emotes"),
                aliases: new string[] { "-o", "--output_dir" },
                description: "Output directory, will be created if it doesn't exist and defaults to current directory if not provided"
            );
            Option version = new Option<bool>(
                defaultValue: false,
                aliases: new string[] { "-v", "--version" },
                description: "Print version"
            );
            Option verbose = new Option<bool>(
                defaultValue: false,
                aliases: new string[] { "--verbose" },
                description: "Enables verbose output, intended for debugging purposes"
            );
            rootCommand.AddOption(platform);
            rootCommand.AddOption(client_id);
            rootCommand.AddOption(client_secret);
            rootCommand.AddOption(token);
            rootCommand.AddOption(channel_ids);
            rootCommand.AddOption(channel_names);
            rootCommand.AddOption(output_dir);
            rootCommand.AddOption(version);
            rootCommand.AddOption(verbose);

            Func<string, string, string, string, string, string, string, bool, bool, IConsole, int> downloadEmotes = (platform, client_id, client_secret, token, channel_ids, channel_names, output_dir, verbose, version, console) =>
            {
                #region Validation
                bool useNames = false;
                bool tokenRequired = true;
                if (version)
                {
                    console.Out.Write($"\nEmoteDownloader v{Program.version}");
                    return 2;
                }
                if (verbose)
                {
                    console.Out.Write("Enabled verbose output\n");
                    //write all parameters to console
                    console.Out.Write($"Platform: {platform}\n");
                    console.Out.Write($"Client ID: {client_id}\n");
                    console.Out.Write($"Client Secret: {client_secret}\n");
                    console.Out.Write($"Token: {token}\n");
                    console.Out.Write($"Channel IDs: {channel_ids}\n");
                    console.Out.Write($"Channel Names: {channel_names}\n");
                    console.Out.Write($"Output Directory: {output_dir}\n");
                    console.Out.Write($"Verbose: {verbose}\n");
                    console.Out.Write($"Version: {version}\n");
                }
                if (platform == null)
                {
                    console.Error.Write("Platform is required\n");
                    return 1;
                }
                else if (platform.ToLower() != "twitch" && platform.ToLower() != "bttv" && platform.ToLower() != "ffz" && platform.ToLower() != "7tv")
                {
                    console.Error.Write($"Invalid platform: {platform}\n");
                    return 1;
                }
                if ((platform.ToLower() == "twitch" || channel_ids == null) && (client_secret == null && token == null) && client_id == null)
                {
                    console.Error.Write("Client ID and Client secret or token and Client ID is required for Twitch or when using channel names\n");
                    return 1;
                }
                if (channel_ids == null && channel_names == null)
                {
                    console.Error.Write("Channel IDs or channel names is required\n");
                    return 1;
                }
                if (channel_ids != null && channel_names != null)
                {
                    if (verbose)
                    {
                        console.Out.Write("Channel IDs and channel names provided, using channel IDs\n");
                    }
                    useNames = false;
                }
                if (channel_ids != null && channel_names == null)
                {
                    if (verbose)
                    {
                        console.Out.Write("Channel IDs provided, using channel IDs\n");
                    }
                    useNames = false;
                }
                if (channel_names != null && channel_ids == null)
                {
                    if (verbose)
                    {
                        console.Out.Write("Channel names provided, using channel names\n");
                    }
                    useNames = true;
                }
                if (channel_ids != null && channel_ids.Split(',').Length == 0)
                {
                    console.Error.Write("Channel IDs cannot be empty\n");
                    return 1;
                }
                if (channel_names != null && channel_names.Split(',').Length == 0)
                {
                    console.Error.Write("Channel names cannot be empty\n");
                    return 1;
                }
                if (output_dir == null)
                {
                    if (verbose)
                    {
                        console.Out.Write("Output directory not provided, using current directory\n");
                    }
                }
                else if (output_dir.Equals(""))
                {
                    output_dir = Path.Combine(Directory.GetCurrentDirectory(), "emotes");
                }
                else if (!Directory.Exists(output_dir))
                {
                    if (verbose)
                    {
                        console.Out.Write($"Output directory does not exist, creating: {output_dir}\n");
                    }
                    Directory.CreateDirectory(output_dir);
                }
                tokenRequired = (platform.ToLower() == "twitch" || useNames);
                #endregion
                #region preparing
                if (verbose)
                {
                    console.Out.Write("Preparing...\n");
                }
                List<string> channel_ids_list = new List<string>();
                if (tokenRequired && token == null)
                {
                    if (verbose)
                    {
                        console.Out.Write("Token not provided, using client ID and secret to get a token\n");
                    }
                    token = TwitchGetToken(client_id, client_secret).GetAwaiter().GetResult();
                    if (token == null)
                    {
                        console.Error.Write("Failed to get token\nPlease ensure that client ID and secret are correct\n");
                        return 3;
                    }
                    if (verbose)
                    {
                        console.Out.Write($"Got Token: {token}\n");
                    }
                }
                if (useNames)
                {
                    if (verbose)
                    {
                        console.Out.Write($"Getting channel IDs from channel names\n");
                    }
                    string url = "https://api.twitch.tv/helix/users?login=" + channel_names.Replace(",", "&login=");
                    string userJson = GetApiJson(url, token, client_id, true).GetAwaiter().GetResult();
                    if (userJson == null)
                    {
                        console.Error.Write("Failed to get channel IDs due to an API error\n");
                        return 3;
                    }
                    if (verbose)
                    {
                        console.Out.Write($"Got channel IDs from channel names\n");
                    }
                    var user = JObject.Parse(userJson);
                    foreach (var user_id in user["data"])
                    {
                        channel_ids_list.Add(user_id["id"].ToString());
                    }
                }
                else
                {
                    channel_ids_list.AddRange(channel_ids.Split(','));
                }
                if (verbose)
                {
                    console.Out.Write($"Got {channel_ids_list.Count} channel IDs\n");
                }
                #endregion
                #region Getting emotes



                //TO DO: Add support for animated emotes (v1.1.0)
                //Extend Dictionary to <Emote, bool> to indicate if it's animated


                if (verbose)
                {
                    console.Out.Write("Getting emotes...\n");
                }
                Dictionary<string, string> emotes = new Dictionary<string, string>();
                foreach (var channel_id in channel_ids_list)
                {
                    if (verbose)
                    {
                        console.Out.Write($"Getting emotes for channel ID: {channel_id}\n");
                    }
                    string url = "";
                    if (platform.ToLower() == "twitch")
                    {
                        url = $"https://api.twitch.tv/helix/chat/emotes?broadcaster_id={channel_id}";
                    }
                    else if (platform.ToLower() == "bttv")
                    {
                        url = $"https://api.betterttv.net/3/cached/users/twitch/{channel_id}";
                    }
                    else if (platform.ToLower() == "ffz")
                    {
                        url = $"https://api.betterttv.net/3/cached/frankerfacez/users/twitch/{channel_id}";
                    }
                    else if (platform.ToLower() == "7tv")
                    {
                        url = $"https://api.7tv.app/v2/users/{channel_id}/emotes";
                    }
                    string emotesJson = GetApiJson(url, token, client_id, platform.ToLower() == "twitch").GetAwaiter().GetResult();
                    if (emotesJson == null)
                    {
                        console.Error.Write("Failed to get emotes due to an API error\n");
                        return 3;
                    }
                    if (verbose)
                    {
                        console.Out.Write($"Got emotesJson for channel ID: {channel_id}\n");
                    }
                    JObject emotesObj;
                    if (platform.ToLower() == "ffz" || platform.ToLower() == "7tv")
                    {
                        emotesObj = JObject.Parse("{\"data\": " + emotesJson + "}");
                    }
                    else
                    {
                        emotesObj = JObject.Parse(emotesJson);
                    }
                    if (platform.ToLower() == "bttv")
                    {
                        if (emotesObj["channelEmotes"] != null)
                        {
                            foreach (var emote in emotesObj["channelEmotes"])
                            {
                                if(!emotes.ContainsKey(emote["code"].ToString()))
                                    emotes.Add(emote["code"].ToString(), bttvLink(emote["id"].ToString()));
                            }
                        }
                        if (emotesObj["sharedEmotes"] != null)
                        {
                            foreach (var emote in emotesObj["sharedEmotes"])
                            {
                                if (!emotes.ContainsKey(emote["code"].ToString()))
                                    emotes.Add(emote["code"].ToString(), bttvLink(emote["id"].ToString()));
                            }
                        }
                    }
                    else
                    {
                        foreach (var emote in emotesObj["data"])
                        {
                            string link = "";
                            if (platform.ToLower() == "twitch")
                            {
                                if (emote["images"]["url_4x"] != null)
                                {
                                    link = emote["images"]["url_4x"].ToString();
                                }
                                else if (emote["images"]["url_2x"] != null)
                                {
                                    link = emote["images"]["url_2x"].ToString();
                                }
                                else if (emote["images"]["url_1x"] != null)
                                {
                                    link = emote["images"]["url_1x"].ToString();
                                }
                                else
                                {
                                    if(verbose)
                                    {
                                        console.Out.Write($"Unable get url for {emote["code"].ToString()}\n");
                                    }
                                    continue;
                                }
                            }
                            else if(platform.ToLower() == "7tv"){
                                if(emote["urls"] != null){
                                    foreach(var item in emote["urls"]){
                                        if(item[1] != null){
                                            link = item[1].ToString();
                                        }
                                    }
                                }
                            }
                            else if(platform.ToLower() == "ffz"){
                                link = $"https://cdn.betterttv.net/frankerfacez_emote/{emote["id"]}/4";
                            }
                            if (!emotes.ContainsKey(emote["code"].ToString()))
                                emotes.Add(emote["name"].ToString(), link);
                        }
                    }
                }
                #endregion
                console.Out.Write("Download started\n");
                Task.WaitAll(downloadEmotesAsync(emotes, output_dir).ToArray());
                console.Out.Write("Download complete!\n");
                return 0;
            };
            rootCommand.Handler = CommandHandler.Create(downloadEmotes);
            return await rootCommand.InvokeAsync(args);
        }
        private static string bttvLink(string id)
        {
            return $"https://cdn.betterttv.net/emote/{id}/3x";
        }
        private static async Task<string> TwitchGetToken(string client_id, string client_secret)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token?client_id=" + client_id + "&client_secret=" + client_secret + "&grant_type=client_credentials"))
            {
                using (var client = new HttpClient())
                {
                    using (var response = await client.SendAsync(request))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var obj = JObject.Parse(json);
                            if (obj["access_token"] != null)
                            {
                                return obj["access_token"].ToString();
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }
        private static async Task<string> GetApiJson(string url, string token, string clientID, bool twitch = false)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (twitch)
                {
                    request.Headers.Add("Authorization", "Bearer " + token);
                    request.Headers.Add("Client-Id", clientID);
                }
                using (var client = new HttpClient())
                {
                    using (var response = await client.SendAsync(request))
                    {
                        return await response.Content.ReadAsStringAsync();
                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsStringAsync();
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }
        private static Task<string> GetApiJsonSync(string url, string token, string clientID, bool twitch = false)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (twitch)
                {
                    request.Headers.Add("Authorization", "Bearer " + token);
                    request.Headers.Add("Client-Id", clientID);
                }
                using (var client = new HttpClient())
                {
                    using (var response = client.SendAsync(request).Result)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return response.Content.ReadAsStringAsync();
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }
        private static List<Task> downloadEmotesAsync(Dictionary<string, string> d, string output){
            List<Task> taskList = new List<Task>();
            foreach (var item in d)
            {
                WebClient wc = new WebClient();
                taskList.Add(wc.DownloadFileTaskAsync(item.Value, Path.Combine(output, item.Key + ".png")));
            }
            return taskList;
        }
    }
}
