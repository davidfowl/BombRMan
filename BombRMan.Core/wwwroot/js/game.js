(function ($, window) {
    var MAP_WIDTH = 15,
        MAP_HEIGHT = 13,
        TILE_SIZE = 32,
        keyState = new Array(8).fill(0),
        prevKeyState = new Array(8).fill(0),
        inputId = 0,
        lastSentInputId = 0,
        lastProcessed = 0,
        lastProcessedTime = 0,
        lastProcessedRTT = 0,
        serverStats,
        inputs = [];

    function getKeyState(key) {
        var index = key >> 5;
        var bit = 1 << (key & 0x1f);
        return (keyState[index] & bit) == bit;
    }

    function getPrevKeyState(key) {
        var index = key >> 5;
        var bit = 1 << (key & 0x1f);
        return (prevKeyState[index] & bit) == bit;
    }

    function setKeyState(key, flag) {
        var index = key >> 5;
        var bit = 1 << (key & 0x1f);
        if (flag === true) {
            keyState[index] |= bit;
        } else {
            keyState[index] &= ~bit;
        }
    }

    function setPrevKeyState(key, flag) {
        var index = key >> 5;
        var bit = 1 << (key & 0x1f);
        if (flag === true) {
            prevKeyState[index] |= bit;
        } else {
            prevKeyState[index] &= ~bit;
        }
    }

    function empty(state) {
        // Everything false means the values are all 0
        for (var b of state) {
            if (b !== 0) {
                return false;
            }
        }
        return true;
    }

    window.Game.Engine = function (assetManager) {
        this.gameServer = new window.signalR.HubConnectionBuilder()
            .withUrl('/game')
            .build();

        this.assetManager = assetManager;
        this.players = {};
        this.ticks = 0;
        this.map = new window.Game.Map(MAP_WIDTH, MAP_HEIGHT, TILE_SIZE);
        this.sprites = [];
        this.inputManager = {
            isKeyDown: function (key) {
                return getKeyState(key) === true;
            },
            isKeyUp: function (key) {
                return getKeyState(key) === false;
            },
            isHoldingKey: function (key) {
                return getPrevKeyState(key) === true &&
                    getKeyState(key) === true;
            },
            isKeyPress: function (key) {
                return getPrevKeyState(key) === false &&
                    getKeyState(key) === true;
            },
            isKeyRelease: function (key) {
                return getPrevKeyState(key) === true &&
                    getKeyState(key) === false;
            }
        };

        for (const keyCode of Object.values(window.Game.Keys)) {
            setKeyState(keyCode, false);
            setPrevKeyState(keyCode, false);
        }

        this.types = {
            GRASS: 0,
            WALL: 2,
            BRICK: 3,
        };

        this.bombSprites = {};
        this.powerupSprites = {};
        this.roundState = 'WaitingForPlayers';
        this.roundMessage = '';

        this.getDebugState = function () {
            var players = {};

            for (var index in this.players) {
                var player = this.players[index];
                if (player) {
                    players[index] = {
                        x: player.x,
                        y: player.y,
                        exactX: player.exactX,
                        exactY: player.exactY,
                        direction: player.direction,
                        bombs: player.bombs,
                        maxBombs: player.maxBombs,
                        power: player.power,
                        speed: player.speed,
                        eliminated: !!player.eliminated
                    };
                }
            }

            return {
                connectionState: this.gameServer.state,
                fps: window.Game.TicksPerSecond,
                roundState: this.roundState,
                roundMessage: this.roundMessage,
                playerIndex: this.playerIndex,
                players: players,
                predictedPlayer: this.playerIndex === undefined ? null : players[this.playerIndex],
                serverPlayer: this.ghost ? {
                    x: this.ghost.x,
                    y: this.ghost.y,
                    exactX: this.ghost.exactX,
                    exactY: this.ghost.exactY,
                    direction: this.ghost.direction
                } : null,
                map: {
                    width: this.map.width,
                    height: this.map.height,
                    tileSize: this.map.tileSize,
                    tiles: this.map.snapshot()
                },
                sprites: this.sprites.map(function (sprite) {
                    return {
                        type: sprite.type,
                        x: sprite.x,
                        y: sprite.y,
                        ticks: sprite.ticks,
                        powerupType: sprite.powerupType
                    };
                }),
                network: {
                    lastInputId: inputId - 1,
                    lastSentInputId: lastSentInputId,
                    lastProcessedInputId: lastProcessed,
                    lastProcessedTime: lastProcessedTime,
                    rtt: lastProcessedRTT,
                    serverStats: serverStats
                }
            };
        };
    };

    window.Game.Engine.prototype = {
        onKeydown: function (e) {
            setKeyState(e.keyCode, true);
        },
        onKeyup: function (e) {
            setKeyState(e.keyCode, false);
        },
        getSpritesAt: function (x, y) {
            var sprites = [];
            for (var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if (sprite.x === x && sprite.y === y) {
                    sprites.push(sprite);
                }
            }
            return sprites;
        },
        addSprite: function (sprite) {
            this.sprites.push(sprite);
            this.sprites.sort(function (a, b) {
                return a.order - b.order;
            });
        },
        removeSprite: function (sprite) {
            var index = window.Game.Utils.indexOf(this.sprites, sprite);
            if (index !== -1) {
                this.sprites.splice(index, 1);
                this.sprites.sort(function (a, b) {
                    return a.order - b.order;
                });
            }
        },
        sendKeyState: function () {

            if (!(empty(prevKeyState) && empty(keyState))) {
                inputs.push({ keyState: keyState, id: inputId++, time: performance.now() });
            }

            var buffer = inputs.splice(0, inputs.length);
            if (buffer.length > 0) {
                this.gameServer.send('sendKeys', buffer);
                lastSentInputId = buffer[buffer.length - 1].id;
            }
        },
        initialize: function () {
            var that = this;

            this.gameServer.on('initializeMap', function (data) {
                that.map.fill(data);
            });

            this.gameServer.on('initializePlayer', function (player) {
                var bomber = new window.Game.Bomber();
                that.playerIndex = player.index;
                that.players[player.index] = bomber;
                bomber.moveTo(player.x, player.y);
                that.addSprite(bomber);


                // Create a ghost
                var ghost = new window.Game.Bomber(false);
                ghost.transparent = true;
                that.ghost = ghost;
                ghost.moveTo(player.x, player.y);
                that.addSprite(ghost);
            });

            this.gameServer.on('playerLeft', function (player) {
                var bomber = that.players[player.index];
                if (bomber) {
                    that.removeSprite(bomber);
                    that.players[player.index] = null;
                }
            });

            this.gameServer.on('initialize', function (players) {
                for (var i = 0; i < players.length; ++i) {
                    var player = players[i];
                    if (that.players[player.index]) {
                        continue;
                    }

                    var bomber = new window.Game.Bomber(false);
                    that.players[player.index] = bomber;
                    bomber.moveTo(players[i].x, players[i].y);
                    that.addSprite(bomber);
                }
            });

            this.gameServer.on('roundReset', function (data) {
                that.clearTransientSprites();
                that.map.fill(data.map);

                for (var i = 0; i < data.players.length; ++i) {
                    var player = data.players[i];
                    var bomber = that.players[player.index];

                    if (!bomber) {
                        bomber = new window.Game.Bomber(false);
                        that.players[player.index] = bomber;
                        that.addSprite(bomber);
                    }

                    bomber.eliminated = false;
                    bomber.maxBombs = player.maxBombs;
                    bomber.power = player.powerLevel;
                    bomber.speed = player.speed;
                    bomber.activeBombs = player.activeBombs;
                    bomber.moveTo(player.x, player.y);
                    bomber.updateAnimation(that);
                }

                if (that.ghost) {
                    var localPlayer = data.players[that.playerIndex];
                    if (localPlayer) {
                        that.ghost.eliminated = false;
                        that.ghost.moveTo(localPlayer.x, localPlayer.y);
                        that.ghost.updateAnimation(that);
                    }
                }

                that.roundState = data.roundState;
                that.roundMessage = '';
            });

            this.gameServer.on('updatePlayerState', function (player) {
                function applyPlayerState(sprite) {
                    if (!sprite) {
                        return;
                    }

                    sprite.x = player.x;
                    sprite.y = player.y;
                    sprite.exactX = player.exactX;
                    sprite.exactY = player.exactY;
                    sprite.direction = player.direction;
                    sprite.directionX = player.directionX;
                    sprite.directionY = player.directionY;
                    sprite.updateAnimation(that);
                }

                if (player.index === that.playerIndex) {
                    lastProcessed = player.lastProcessed;
                    lastProcessedTime = player.lastProcessedTime;

                    applyPlayerState(that.ghost);
                    applyPlayerState(that.players[player.index]);
                }
                else {
                    applyPlayerState(that.players[player.index]);
                }
            });

            this.gameServer.on('serverStats', stats => {
                serverStats = stats;
            });

            // Server-authoritative bomb/explosion/powerup/round events. The client only
            // renders sprites/tiles/state pushed by the server - it never simulates them.
            this.gameServer.on('initializeBombs', function (bombs) {
                for (var i = 0; i < bombs.length; ++i) {
                    that.addBombSprite(bombs[i]);
                }
            });

            this.gameServer.on('initializeExplosions', function (explosions) {
                for (var i = 0; i < explosions.length; ++i) {
                    that.addSprite(that.createExplosionSprite(explosions[i].x, explosions[i].y));
                }
            });

            this.gameServer.on('initializePowerups', function (powerups) {
                for (var i = 0; i < powerups.length; ++i) {
                    that.addPowerupSprite(powerups[i]);
                }
            });

            this.gameServer.on('bombPlaced', function (bomb) {
                that.addBombSprite(bomb);
            });

            this.gameServer.on('bombExploded', function (data) {
                var bombSprite = that.bombSprites[data.bombId];
                if (bombSprite) {
                    that.removeSprite(bombSprite);
                    delete that.bombSprites[data.bombId];
                }

                for (var i = 0; i < data.tiles.length; ++i) {
                    that.addSprite(that.createExplosionSprite(data.tiles[i].x, data.tiles[i].y));
                }
            });

            this.gameServer.on('mapTileChanged', function (change) {
                that.map.set(change.x, change.y, change.tile);
            });

            this.gameServer.on('powerupSpawned', function (powerup) {
                that.addPowerupSprite(powerup);
            });

            this.gameServer.on('powerupCollected', function (data) {
                var key = that.powerupKey(data.x, data.y);
                var sprite = that.powerupSprites[key];
                if (sprite) {
                    that.removeSprite(sprite);
                    delete that.powerupSprites[key];
                }
            });

            this.gameServer.on('playerEliminated', function (player) {
                var bomber = player.index === that.playerIndex ? that.ghost : that.players[player.index];
                if (bomber) {
                    bomber.eliminated = true;
                }
            });

            this.gameServer.on('roundStarted', function () {
                that.roundState = 'InProgress';
                that.roundMessage = '';
            });

            this.gameServer.on('roundOver', function (data) {
                that.roundState = 'RoundOver';
                that.roundMessage = data.winnerIndex === null || data.winnerIndex === undefined
                    ? 'Draw!'
                    : 'Player ' + data.winnerIndex + ' wins!';
            });

            this.gameServer.start();
        },
        powerupKey: function (x, y) {
            return x + ',' + y;
        },
        clearTransientSprites: function () {
            for (var i = this.sprites.length - 1; i >= 0; --i) {
                var sprite = this.sprites[i];
                if (sprite.type === window.Game.Sprites.BOMB ||
                    sprite.type === window.Game.Sprites.EXPLOSION ||
                    sprite.type === window.Game.Sprites.POWERUP) {
                    this.sprites.splice(i, 1);
                }
            }

            this.bombSprites = {};
            this.powerupSprites = {};
        },
        createExplosionSprite: function (x, y) {
            var game = this;
            // Purely cosmetic countdown - the server is the source of truth for whether a
            // tile is dangerous; this only controls how long the visual effect lingers.
            return {
                type: window.Game.Sprites.EXPLOSION,
                order: 1,
                x: x,
                y: y,
                ticks: window.Game.TicksPerSecond,
                update: function () {
                    this.ticks--;
                    if (this.ticks <= 0) {
                        game.removeSprite(this);
                    }
                }
            };
        },
        addBombSprite: function (bomb) {
            var sprite = {
                type: window.Game.Sprites.BOMB,
                order: 0,
                x: bomb.x,
                y: bomb.y
            };
            this.bombSprites[bomb.id] = sprite;
            this.addSprite(sprite);
        },
        addPowerupSprite: function (powerup) {
            var sprite = {
                type: window.Game.Sprites.POWERUP,
                order: 1,
                x: powerup.x,
                y: powerup.y,
                powerupType: powerup.type
            };
            this.powerupSprites[this.powerupKey(powerup.x, powerup.y)] = sprite;
            this.addSprite(sprite);
        },
        update: function () {
            this.ticks++;
            this.sendKeyState();

            if (this.inputManager.isKeyPress(window.Game.Keys.D)) {
                window.Game.Debugging = !window.Game.Debugging;
            }

            if (this.inputManager.isKeyPress(window.Game.Keys.P)) {
                window.Game.MoveSprites = !window.Game.MoveSprites;
            }

            for (var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if (sprite.update) {
                    sprite.update(this);
                }
            }

            prevKeyState = [...keyState];

            window.Game.Logger.log('last input = ' + (inputId - 1));
            window.Game.Logger.log('last sent input = ' + lastSentInputId);
            window.Game.Logger.log('last server processed input = ' + lastProcessed);
            if (lastProcessed < lastSentInputId) {
                lastProcessedRTT = performance.now() - lastProcessedTime;
            }
            window.Game.Logger.log('last server processed input time (ms) = ' + lastProcessedRTT);
            if (serverStats) {
                window.Game.Logger.log('server updates/s = ' + serverStats.updates + ', processed inputs/s = ' + serverStats.processedInputs);
                window.Game.Logger.log('server queue depth = ' + serverStats.queueDepth + ' (max ' + serverStats.maxQueueDepth + '), dropped inputs = ' + serverStats.droppedInputs);
                window.Game.Logger.log('server tick overruns = ' + serverStats.tickOverruns + ', max tick delta (ms) = ' + serverStats.maxTickDeltaMs);
                window.Game.Logger.log('server players = ' + serverStats.activePlayers + ' active, ' + serverStats.availablePlayerSlots + ' available');
            }
            window.Game.Logger.log('serverStats:' + JSON.stringify(serverStats));
        },
        movable: function (x, y) {
            if (y >= 0 && y < MAP_HEIGHT && x >= 0 && x < MAP_WIDTH) {
                if (this.map.get(x, y) === this.types.GRASS) {
                    for (var i = 0; i < this.sprites.length; ++i) {
                        var sprite = this.sprites[i];
                        if (sprite.x === x && sprite.y === y && sprite.type === window.Game.Sprites.BOMB) {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

    };

})(jQuery, window);