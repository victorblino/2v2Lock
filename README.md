# 2v2Lock

`2v2Lock` is a Deadlock training plugin focused on repeatable 2v2 lane practice.

Players join the server, get auto-balanced into teams, choose their heroes, and type `/ready` in chat. When everyone is ready, the plugin picks a random lane, reloads the map, and starts a live round. The win condition is simple: destroy the enemy lane objective first.

The project is still a work in progress, but it is already fully playable.

## Features

- Automatic 2v2 team balancing
- Random hero on join, with `/hero <name>` to switch in lobby
- Ready system with lobby flow
- Random lane selection for each round
- Automatic round reset after a winner is decided
- Configurable debug mode through `config.json`

## Commands

- `/hero`
- `/hero random`
- `/hero <name>`
- `/ready`
- `/unready`

## Configuration

`config.json`

```json
{
  "debug": false
}
```

- `debug: false` keeps the normal 4-player flow
- `debug: true` allows partial-lobby testing

## Build

The project targets `.NET 10` and references `DeadworksManaged.Api`.

For local development, the plugin builds against your local Deadlock install by default.

For GitHub Actions, the workflow clones the official [Deadworks](https://github.com/Deadworks-net/deadworks) source, builds `DeadworksManaged.Api`, then builds and packages this plugin automatically.

## Packaging

The workflow creates this structure:

```text
plugins/
└── 2v2Lock/
    ├── 2v2Lock.dll
    ├── 2v2Lock.pdb
    └── config.json
```

It also uploads a zip artifact containing the packaged plugin folder.

## Status

This plugin is under active development. The current focus is making 2v2 lane practice fast to restart, easy to host, and reliable for repeated training sessions.
