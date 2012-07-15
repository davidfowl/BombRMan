(function($, window) {
    var MAP_WIDTH = 15,
        MAP_HEIGHT = 13;

    window.Game.Engine = function() {
        this.map = new window.Game.Map(MAP_WIDTH, MAP_HEIGHT);
        this.sprites = [];

        this.types = {
            GRASS: 0,
            WALL: 2,
            BRICK: 3,
        };

    var s = "222222222222222" +
            "200003030300002" +
            "202323232323202" +
            "203333333333302" +
            "202323232323202" +
            "233333333333332" +
            "232323232323232" +
            "233333333333332" +
            "202323232323202" +
            "203333333333302" +
            "202323232323202" +
            "200003030300002" +
            "222222222222222";

        this.map.fill(s);
        
        // Initialize the bomber
        var bomber = new window.Game.Bomber();
        bomber.moveTo(1, 1);
        this.addSprite(bomber);
    };

    window.Game.Engine.prototype = {
        onInput: function (e) {
            var length = this.sprites.length;
            for(var i = 0; i < length; ++i) {
                var sprite = this.sprites[i];
                if(sprite.onInput) {
                    sprite.onInput(this, e.keyCode);
                }
            }
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
        update : function() {
            for(var i = 0; i < this.sprites.length; ++i) {
                var sprite = this.sprites[i];
                if(sprite.update) {
                    sprite.update(this);
                }
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