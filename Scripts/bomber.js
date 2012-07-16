(function($, window) {
    var DELTA = 50,
        POWER = 100;

    window.Game.Bomber = function() {
        this.x = 0;
        this.y = 0;
        this.discreteX = 0;
        this.discreteY = 0;
        this.type = window.Game.Sprites.BOMBER;
        this.order = 2;
        this.maxBombs = 1;
        this.power = 1;
        this.speed = 1;
        this.direction = window.Game.Direction.SOUTH;
        this.bombs = 0;
        this.bombType = window.Game.Bombs.NORMAL;
    };

    window.Game.Bomber.prototype = {
        createBomb: function(game) {
            if(this.bombs >= this.maxBombs) {
                return;
            }

            this.bombs++;
            var bomb = new window.Game.Bomb(this.x, this.y, 3, this.power, this.bombType, this);
            game.addSprite(bomb);
        },
        onInput: function(game, keyCode) {
            var x = this.discreteX,
                y = this.discreteY,
                handled = false,
                delta = DELTA;


            switch(keyCode) {
                case window.Game.Keys.UP:
                    y -= delta;
                    this.direction = window.Game.Direction.NORTH;
                    handled = true;
                    break;
                case window.Game.Keys.DOWN:
                    y += delta;
                    this.direction = window.Game.Direction.SOUTH;
                    handled = true;
                    break;
                case window.Game.Keys.LEFT:
                    x -= delta;
                    this.direction = window.Game.Direction.WEST;
                    handled = true;
                    break;
                case window.Game.Keys.RIGHT:
                    x += delta;
                    this.direction = window.Game.Direction.EAST;
                    handled = true;
                    break;
                case window.Game.Keys.A:
                    this.createBomb(game);
                    handled = true;
                    break;
            }

            this.moveDiscrete(game, x, y);

            return handled;
        },
        update: function(game) {
            var sprites = game.getSpritesAt(this.x, this.y);
            for(var i = 0; i < sprites.length; ++i) {
                var sprite = sprites[i];
                if(sprite.type === window.Game.Sprites.POWERUP) {
                    switch(sprite.powerupType) {
                        case window.Game.Powerups.SPEED:
                            this.increaseSpeed();
                            break;
                        case window.Game.Powerups.BOMB:
                            this.increaseMaxBombs();
                            break;
                        case window.Game.Powerups.EXPLOSION:
                            this.increasePower();
                            break;
                    }
                    sprite.explode(game);
                }
            }
        },
        explode: function(game) {
            game.removeSprite(this);
        },
        removeBomb: function() {
            this.bombs--;
        },
        increaseSpeed: function() {
            this.speed++;
        },
        increaseMaxBombs: function() {
            this.maxBombs++;
        },
        increasePower: function() {
            this.power++;
        },
        getEffectiveValue : function(value) {
            var mod =(value % POWER);

            if(mod === 0) {
                return value;
            }

            switch(this.direction) {
                case window.Game.Direction.EAST:
                case window.Game.Direction.SOUTH:
                    return value + (POWER - mod);
                case window.Game.Direction.NORTH:
                case window.Game.Direction.WEST:
                    return value - mod;
            }
        },
        moveDiscrete: function(game, x, y) {
            var effectiveX = this.getEffectiveValue(x) / POWER,
                effectiveY = this.getEffectiveValue(y) / POWER;

            if(game.movable(effectiveX, effectiveY)) {

                this.y = Math.floor((y + DELTA) / POWER);
                this.x = Math.floor((x + DELTA) / POWER);

                this.discreteX = x;
                this.discreteY = y;   
            }

            /* console.log('x=' + this.x + 
                        ', y=' + this.y + 
                        ', discreteX=' + (this.discreteX / POWER) + 
                        ', discreteY=' + (this.discreteY / POWER) +
                        ', effectiveX=' + effectiveX +
                        ', effectiveY=' + effectiveY); */
        },
        moveTo: function (x, y) {
            this.discreteX = x * POWER;
            this.discreteY = y * POWER;
            this.x = x;
            this.y = y;
        }
    };

})(jQuery, window);