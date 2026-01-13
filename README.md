<a name="readme-top"></a>

![GitHub tag (with filter)](https://img.shields.io/github/v/tag/Jezus09/k4arenas?style=for-the-badge&label=Version)
![GitHub Repo stars](https://img.shields.io/github/stars/Jezus09/k4arenas?style=for-the-badge)
![GitHub](https://img.shields.io/github/license/Jezus09/k4arenas?style=for-the-badge)
![GitHub last commit](https://img.shields.io/github/last-commit/Jezus09/k4arenas?style=for-the-badge)

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/Jezus09/k4arenas">
    <img src="https://i.imgur.com/sej1ZzD.png" alt="Logo" width="400" height="256">
  </a>
  <h3 align="center">CounterStrike2 | K4-Arenas (Updated)</h3>
  <p align="center">
    An all in one arena plugin, that you can use to have a ladder type gameplay. Support all map, 2v2/3v3/etc. Includes preferences, challenges and developer API for custom rounds.
    <br />
    <strong>Updated to CounterStrikeSharp API 1.0.355</strong>
  </p>
</div>

<!-- ABOUT THE PROJECT -->

## About This Fork

This is an updated version of the original [K4-Arenas plugin](https://github.com/KitsuneLab-Development/K4-Arenas) by K4ryuu. The original repository has been archived and is no longer maintained.

### Updates in this version:
- ✅ **CounterStrikeSharp API updated to v1.0.355** (latest stable release)
- ✅ Fixed hardcoded dependency paths for cross-platform compatibility
- ✅ Updated all project dependencies to latest stable versions
- ✅ .NET 8.0 support maintained
- ✅ Full compatibility with CS2 game update 1.40.8.5 (January 2025)

### Dependencies

To use this server addon, you'll need the following dependencies installed:

- [**CounterStrikeSharp v1.0.355+**](https://github.com/roflmuffin/CounterStrikeSharp/releases): CounterStrikeSharp allows you to write server plugins in C# for Counter-Strike 2/Source2/CS2.
- [**KitsuneMenu**](https://github.com/schwarper/KitsuneMenu): Menu library for center menu support (included in release)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- INSTALLATION -->

## Installation

1. Download the latest release from the [Releases](https://github.com/Jezus09/k4arenas/releases) page
2. Extract the contents to your CS2 server's `game/csgo/addons/counterstrikesharp/` directory
3. The plugin will auto-generate a config file on first run: `configs/plugins/K4-Arenas/K4-Arenas.json`
4. Configure the plugin according to your needs
5. Restart your server

### Database Configuration (Optional)

The plugin supports MySQL database for storing player preferences. Database is **optional** since v2.0.7 - the plugin will work without it, but player weapon and round preferences won't be saved.

To enable database support, edit the config file and add your MySQL credentials:

```json
{
  "database-settings": {
    "Host": "localhost",
    "Port": 3306,
    "Database": "your_database",
    "Username": "your_username",
    "Password": "your_password",
    "Sslmode": "Preferred",
    "TablePrefix": "",
    "TablePurgeDays": 30
  }
}
```

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- BUILDING -->

## Building from Source

### Requirements:
- .NET 8.0 SDK
- Git

### Build Instructions:

```bash
# Clone the repository
git clone https://github.com/Jezus09/k4arenas.git
cd k4arenas

# Build the shared API
cd src-shared
dotnet build -c Release

# Build the main plugin
cd ../src-plugin
dotnet publish -c Release

# Build the bots plugin (optional)
cd ../src-botsplugin
dotnet publish -c Release
```

The compiled plugins will be in:
- Main plugin: `src-plugin/bin/K4-Arenas/`
- Bots plugin: `src-botsplugin/bin/K4-Arenas-Bots/`

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- FEATURES -->

## Features

- 🎯 **Automatic Arena Detection**: Works on any map with spawn points
- 🔫 **Weapon Preferences**: Players can choose their preferred weapons per category
- 🎲 **Multiple Round Types**: Rifle, Sniper, Shotgun, SMG, LMG, Pistol rounds
- 👥 **Team Modes**: Supports 1v1, 2v2, 3v3, and custom team sizes
- ⚔️ **Challenge System**: Players can challenge each other to duels
- 🤖 **Bot Support**: Optional bot arena management plugin
- 🌍 **Multi-language**: English and Polish translations included
- 📊 **Player Statistics**: Optional MySQL database integration
- 🎨 **Customizable Rounds**: Developer API for custom round types
- ⚙️ **Highly Configurable**: Extensive config options for server owners

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- COMMANDS -->

## Commands

### Player Commands:
- `!guns` / `!weaponpref` - Open weapon preference menu
- `!rounds` / `!roundpref` - Open round type preference menu
- `!afk` - Toggle AFK mode
- `!challenge <player>` - Challenge another player to a duel
- `!lang` - Change your language preference

### Admin Commands:
Check the [original K4-Arenas Wiki](https://github.com/KitsuneLab-Development/K4-Arenas/wiki) for detailed admin commands and configuration options.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- COMMUNITY -->

## Community Addons

The community has also created some addons which allows you to extend the functionality of K4-Arenas:

- [**K4-Arenas-Special-Rounds**](https://github.com/Letaryat/K4-Arenas-Special-Rounds): Only Headshot: AK47, Only Headshot: USP-S, Only Headshot: Scout, Only nade

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- CHANGELOG -->

## Changelog

### 2026-01-13 - CSS v1.0.355 Update
- Updated CounterStrikeSharp API to v1.0.355
- Updated Dapper to v2.1.66
- Updated MySqlConnector to v2.5.0
- Fixed hardcoded KitsuneMenu dependency path
- Improved cross-platform compatibility
- Tested and verified compatibility with latest CS2 updates

### v2.0.7 (Original)
- Added weapon preference API function
- Database is now optional (plugin works without DB)
- Various bug fixes and improvements

For full changelog history, see the [original repository](https://github.com/KitsuneLab-Development/K4-Arenas/blob/main/CHANGELOG.md).

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- CREDITS -->

## Credits

### Original Author:
- [**K4ryuu**](https://github.com/K4ryuu) - Created the original K4-Arenas plugin
- [**KitsuneLab Team**](https://github.com/KitsuneLab-Development) - Original development and maintenance

### This Fork:
This fork was created to keep the plugin updated and compatible with the latest CounterStrikeSharp API versions, as the original repository has been archived.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- LICENSE -->

## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information.

This is a fork of the original K4-Arenas plugin, maintaining the same GPL-3.0 license.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- LINKS -->

## Useful Links

- [Original K4-Arenas Repository](https://github.com/KitsuneLab-Development/K4-Arenas)
- [Original Wiki Documentation](https://github.com/KitsuneLab-Development/K4-Arenas/wiki)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [CounterStrikeSharp Documentation](https://docs.cssharp.dev/)

<p align="right">(<a href="#readme-top">back to top</a>)</p>
