(function($, window) {
    var MAP_WIDTH = 15,
        MAP_HEIGHT = 13,
        TILE_SIZE = 32,
        keyState = {},
        prevKeyState = {};

    window.Game.Engine = function(assetManager) {
        this.assetManager = assetManager;
        this.player = null;
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
            keyState[key] = false;
            prevKeyState[key] = false;
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
        initialize: function() {
            var that = this,
                gameServer = $.connection.gameServer;

            gameServer.initializeMap = function(data) {
                that.map.fill(data);
            };

            gameServer.initializePlayer = function(player) {
                that.player = player;

                var bomber = new window.Game.Bomber();
                bomber.moveTo(player.X, player.Y);
                that.addSprite(bomber);
            };

            gameServer.initialize = function(players) {
                for(var i = 0; i < players.length; ++i) {
                    if(that.player && players[i].Index === that.player.Index) {
                        continue;
                    }

                    var bomber = new window.Game.RemoteBomber();
                    bomber.moveTo(players[i].X, players[i].Y);
                    that.addSprite(bomber);
                }
            };

            $.connection.hub.logging = true;
            $.connection.hub.start();
        },
        update : function() {
            if(this.inputManager.isKeyPress(window.Game.Keys.D)) {
                window.Game.Debugging = !window.Game.Debugging;
            }

            if(this.inputManager.isKeyPress(window.Game.Keys.P)) {
                window.Game.MoveSprites = !window.Game.MoveSprites;
            }

            for(var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if(sprite.update) {
                    sprite.update(this);
                }
            }

            for(var key in keyState) {
                prevKeyState[key] = keyState[key];
            }
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