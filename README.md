<a href="https://discord.gg/Xwp5eun7w7" target="_blank">![Discord Badge](https://img.shields.io/discord/1429263454698602629?style=for-the-badge&logo=discord&logoColor=white&label=Discord&labelColor=5865F2&theme=clean&compact=true&color=gray)</a> ![Last Updated Badge](https://img.shields.io/github/last-commit/fabiantomischka/cs2ctf?style=for-the-badge&logoColor=white&label=Latest%20Update&labelColor=48b247&theme=clean&compact=true&color=gray)

> [!WARNING]  
> **We are only a few days away from release! Do not use the mod on live servers yet.**

# About CS2 CTF

CS2 CTF is a [Counter Strike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) mod which adds back the old Capture The Flag mode. Both teams do own a flag and each team needs to return the flag of the opponent to their own base. CS2 CTF currently does support

## Current Features & Development Progress

- [x] Spawning flags for each team with an easy editor
- [x] Picking up flags for each team
- [x] Respawning when a player was killed
- [x] If a flag was lost, return it either after X seconds as set by the ConVar or return it if a team member touched it and play the return sound plus text message
- [x] Awarding MVPs for secured flags
- [x] Securing flags, including playing a sound and chat messages
- [x] Awarding a point per secured flag
- [x] When the scorelimit is hit as per the config, the endscreen is shown (won/lost) and game freezes everybody and re-starts the map after 30 seconds

**Required todo for core gameplay**

- [ ] The editor does need a command to create spawns, as default spawns will usually not work for this type of game mode
- [ ] Editor needs to save the base location as well as spawn locations into a .json file for every map
- [ ] If CTF is enabled on map start, it needs to load the locations from the editor and spawn entities
- [ ] Buyzones need to be added dynamically around the bases
- [ ] Existing buyzones, hostage zones and bomb zones need to be removed from the maps at map start (They sometimes prompt messages)

**What would be nice to include the vanilla gameplay feeling and extension**
- [ ] Check for info_target. There should be at least 2 entities on the map with the name ctf_ct_base and ctf_t_base marking the flag locations, then taking buyzones, spawns and such into account for CTF
- [ ] Teleport the top 5 players of each team towards the end screen positions as in the vanilla gameplay

## Installation
Check out our [Installation Guide](https://cs2ctf.gitbook.io/docs/getting-started/installation) in the documentation for a step by step setup!

> [!WARNING]  
> **There is currently no pre-built version available as this gamemode is not production ready and still in development**

## Documentation
More information and help can be found in our [documentation](https://cs2ctf.gitbook.io/docs)!

## Community & Support
If you have checked the documentation and require additional help, want to be part of the community or just want to chat: [Join us on Discord](https://discord.gg/Xwp5eun7w7)!

## Contributing
Thank you for your interest in contributing! If you want to move forward, please refer to our [Contribution Guidelines](CONTRIBUTING.md)!

## Code of Conduct
In order to ensure that the community is welcoming to all, please review and abide by our [Code of Conduct](CODE_OF_CONDUCT.md)!

## Security Vulnerabilities
If you discover a security vulnerability within our code, please contact the team on [Discord](https://discord.gg/Xwp5eun7w7)!

## License
CS2 CTF is open source software licensed under the [MIT license](LICENSE.md)!
