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
    };

    window.Game.Engine.prototype = {
        onKeydown: function (e) {
            setKeyState(e.keyCode, true);
        },
        onKeyup: function (e) {
            setKeyState(e.keyCode, false);
        },
        onExplosionEnd: function (x, y) {
            var randomPower = Math.floor(Math.random() * window.Game.Powerups.EXPLOSION) + window.Game.Powerups.SPEED;

            if (this.map.get(x, y) === this.types.BRICK) {
                this.map.set(x, y, this.types.GRASS);

                this.addSprite(new window.Game.Powerup(x, y, 5, randomPower));
            }
        },
        onExplosion: function (x, y) {
            for (var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if (sprite.explode && sprite.x === x && sprite.y === y) {
                    sprite.explode(this);
                }
            }
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
        canDestroy: function (x, y) {
            var tile = this.map.get(x, y);
            return tile === this.types.BRICK || tile === this.types.GRASS;
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

            this.gameServer.on('updatePlayerState', function (player) {
                var sprite = null;
                if (player.index === that.playerIndex) {
                    sprite = that.ghost;
                    lastProcessed = player.lastProcessed;
                    lastProcessedTime = player.lastProcessedTime;
                }
                else {
                    sprite = that.players[player.index];
                }

                if (sprite) {
                    // Brute force
                    sprite.x = player.x;
                    sprite.y = player.y;
                    sprite.exactX = player.exactX;
                    sprite.exactY = player.exactY;
                    sprite.direction = player.direction;
                    sprite.directionX = player.directionX;
                    sprite.directionY = player.directionY;
                    sprite.updateAnimation(that);
                }
            });

            this.gameServer.on('serverStats', stats => {
                serverStats = stats;
            });

            this.gameServer.start();
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