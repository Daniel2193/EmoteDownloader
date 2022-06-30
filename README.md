# EmoteDownloader
Bulk download emotes from Twitch/BTTV/FFZ/7TV

License: GPL-3.0


# Usage
```
Usage:
  EmoteDownloader [options]

Options:
  -p, --platform <p>                 Platform to download from. Valid values: twitch, bttv, ffz, 7tv
  --client_id <client_id>            Client ID, not required if token is provided or platform is not twitch and
                                     channel_ids is provided
  --client_secret <client_secret>    Client Secret, not required if token is provided or platform is not twitch and
                                     channel_ids is provided
  -t, --token <t>                    Token, not required if client ID and secret are provided or platform is not
                                     twitch and channel_ids is provided
  --channel_ids <channel_ids>        Channel IDs, separated by commas. Not required if channel names are provided but
  --channel_names <channel_names>    Channel Names, separated by commas. Not required if channel IDs are provided
  -o, --output_dir <o>               Output directory, will be created if it doesn't exist and defaults to current
                                     directory if not provided
  -v, --version                      Print version
  --verbose                          Enables verbose output, intended for debugging purposes
  --help                             Prints this
```

# How to get a Client_ID / Client_Secret?

- Go to https://dev.twitch.tv/console/apps and create a new Application

- Make sure to save the Client_Secret somewhere since you cannot view it again without resetting it
