(function($, window) {
    var MAP_WIDTH = 15,
        MAP_HEIGHT = 13,
        TILE_SIZE = 32,
        keyState = {},
        prevKeyState = {},
        inputId = 0,
        lastProcessed = 0,
        serverInput = 0,
        inputs = [],
        localInput = 0;


    function empty(state) {
        for(var key in window.Game.Keys) {
            if(state[window.Game.Keys[key]] === true) {
                return false;
            }
        }
        return true;
    }

    window.Game.Engine = function(assetManager) {
        this.assetManager = assetManager;
        this.players = {};
        this.ticks = 0;
        this.map = new window.Game.Map(MAP_WIDTH, MAP_HEIGHT, TILE_SIZE);
        this.sprites = [];
        this.inputManager = {
            isKeyDown: function(key) {
                return keyState[key] === true;
            },
            isKeyUp: function(key) {
                return keyState[key] === false;
            },
            isHoldingKey: function(key) {
                return prevKeyState[key] === true &&
                       keyState[key] === true;
            },
            isKeyPress: function(key) {
                return prevKeyState[key] === false &&
                       keyState[key] === true;
            },
            isKeyRelease: function(key) {
                return prevKeyState[key] === true &&
                       keyState[key] === false;
            }
        };
        
        for(var key in window.Game.Keys) {
            keyState[window.Game.Keys[key]] = false;
            prevKeyState[window.Game.Keys[key]] = false;
        }

        this.types = {
            GRASS: 0,
            WALL: 2,
            BRICK: 3,
        };
    };

    window.Game.Engine.prototype = {
        onKeydown: function (e) {
            keyState[e.keyCode] = true;
        },
        onKeyup: function (e) {
            keyState[e.keyCode] = false;
        },
        onExplosionEnd: function(x, y) {
            var randomPower = Math.floor(Math.random() * window.Game.Powerups.EXPLOSION) + window.Game.Powerups.SPEED;

            if(this.map.get(x, y) === this.types.BRICK) {
                this.map.set(x, y, this.types.GRASS);

                this.addSprite(new window.Game.Powerup(x, y, 5, randomPower));
            }
        },
        onExplosion: function(x, y) {
            for(var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if(sprite.explode && sprite.x === x && sprite.y === y) {
                    sprite.explode(this);
                }
            }
        },
        getSpritesAt: function(x, y) {
            var sprites = [];
            for(var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if(sprite.x === x && sprite.y === y) {
                    sprites.push(sprite);
                }
            }
            return sprites;
        },
        canDestroy : function(x, y) {
            var tile = this.map.get(x, y);
            return tile === this.types.BRICK || tile === this.types.GRASS;
        },
        addSprite : function(sprite) {
            this.sprites.push(sprite);
            this.sprites.sort(function(a, b) {
                return a.order - b.order;
            });
        },
        removeSprite: function(sprite) {
            var index = window.Game.Utils.indexOf(this.sprites, sprite);
            if(index !== -1) {
                this.sprites.splice(index, 1);
                this.sprites.sort(function(a, b) {
                    return a.order - b.order;
                });
            }
        },
        sendKeyState: function() {
            var player = this.players[this.playerIndex],
                updateTick = Math.max(1, Math.floor(window.Game.TicksPerSecond / 2));

            if($.connection.hub.state === $.signalR.connectionState.connected) {
                var gameServer = $.connection.gameServer;
                if(this.ticks % updateTick === 0) {                    
                    var got = inputs.length;
                    gameServer.sendKeys(inputs.slice(serverInput, got));
                    serverInput = got;
                }
            }

            // if(empty(prevKeyState) && empty(keyState)) return;

            inputs.push({ keyState: keyState, id: inputId++ });
        },
        initialize: function() {
            var that = this,
                gameServer = $.connection.gameServer;

            gameServer.initializeMap = function(data) {
                that.map.fill(data);
            };

            gameServer.initializePlayer = function(player) {
                var bomber = new window.Game.Bomber();
                that.playerIndex = player.Index;
                that.players[player.Index] = bomber;
                bomber.moveTo(player.X, player.Y);
                that.addSprite(bomber);
                
                
                // Create a ghost
                var ghost = new window.Game.Bomber(false);
                ghost.transparent = true;
                that.ghost = ghost;
                ghost.moveTo(player.X, player.Y);
                that.addSprite(ghost);

                // Create a local ghost
                var local = new window.Game.Bomber();
                local.transparent = true;
                that.local = local;
                local.moveTo(player.X, player.Y);
                that.addSprite(local);
            };

            gameServer.playerLeft = function(player) {
                var bomber = that.players[player.Index];
                if(bomber) {
                    that.removeSprite(bomber);
                    that.players[player.Index] = null;
                }
            };

            gameServer.initialize = function(players) {
                for(var i = 0; i < players.length; ++i) {
                    var player = players[i];
                    if(that.players[player.Index]) {
                        continue;
                    }

                    var bomber = new window.Game.Bomber(false);
                    that.players[player.Index] = bomber;
                    bomber.moveTo(players[i].X, players[i].Y);
                    that.addSprite(bomber);
                }
            };

            gameServer.updatePlayerState = function(player) {
                var sprite = null;
                if(player.Index === that.playerIndex) {
                    sprite = that.ghost;
                    lastProcessed = player.LastProcessed;
                }
                else {
                    sprite = that.players[player.Index];
                }

                if(sprite) {
                    // Brute force
                    sprite.x = player.X;
                    sprite.y = player.Y;
                    sprite.exactX = player.ExactX;
                    sprite.exactY = player.ExactY;
                    sprite.direction = player.Direction;
                    sprite.directionX = player.DirectionX;
                    sprite.directionY = player.DirectionY;
                    sprite.updateAnimation(that);
                }
            };

            $.connection.hub.logging = true;
            // $.connection.hub.url = 'http://localhost:8081/signalr';
            $.connection.hub.start();
        },
        update : function() {
            this.ticks++;

            if(this.inputManager.isKeyPress(window.Game.Keys.D)) {
                window.Game.Debugging = !window.Game.Debugging;
            }

            if(this.inputManager.isKeyPress(window.Game.Keys.P)) {
                window.Game.MoveSprites = !window.Game.MoveSprites;
            }

            for(var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if(sprite.update && sprite !== this.local) {
                    sprite.update(this);
                }
            }

            if(this.local) {            
                var that = this;
                if(localInput < inputs.length) {
                    this.local.update({ assetManager: this.assetManager,
                                        map : this.map,
                                        movable: function(x, y) {
                                            return that.movable(x, y);
                                        },
                                        getSpritesAt: function(x, y) {
                                            return that.getSpritesAt(x, y);
                                        }, 
                                        inputManager: { 
                                            isKeyUp: function(key) {
                                                return inputs[localInput].keyState[key] === false;
                                            },
                                            isKeyDown: function(key) {
                                                return inputs[localInput].keyState[key] === true;
                                            },
                                            isKeyPress: function(key) {
                                                return false;
                                            }
                                        } 
                                      });
                    localInput++;
                }
            }

            this.sendKeyState();

            for(var key in keyState) {
                prevKeyState[key] = keyState[key];
            }

            window.Game.Logger.log('last processed input = ' + lastProcessed);
            window.Game.Logger.log('last sent input = ' + (inputId - 1));
            window.Game.Logger.log('local processed input = ' + localInput);
        },
        movable:  function(x, y) {
            if(y >= 0 && y < MAP_HEIGHT && x >= 0 && x < MAP_WIDTH) {
                if(this.map.get(x, y) === this.types.GRASS) {
                    for(var i = 0; i < this.sprites.length; ++i) {
                        var sprite = this.sprites[i];
                        if(sprite.x === x && sprite.y === y && sprite.type === window.Game.Sprites.BOMB) {
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