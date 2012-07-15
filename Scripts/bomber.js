(function($, window) {
    var DELTA = 1;

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
                handled = false;

            switch(keyCode) {
                case window.Game.Keys.UP:
                    if(game.movable(this.x, this.y - 1)) {
                        y -= DELTA;
                    }
                    this.direction = window.Game.Direction.NORTH;
                    handled = true;
                    break;
                case window.Game.Keys.DOWN:
                    if(game.movable(this.x, this.y + 1)) {
                        y += DELTA;
                    }
                    this.direction = window.Game.Direction.SOUTH;
                    handled = true;
                    break;
                case window.Game.Keys.LEFT:
                    if(game.movable(this.x - 1, this.y)) {
                        x -= DELTA;
                    }
                    this.direction = window.Game.Direction.WEST;
                    handled = true;
                    break;
                case window.Game.Keys.RIGHT:
                    if(game.movable(this.x + 1, this.y)) {
                        x += DELTA;
                    }
                    this.direction = window.Game.Direction.EAST;
                    handled = true;
                    break;
                case window.Game.Keys.A:
                    this.createBomb(game);
                    handled = true;
                    break;
            }

            this.moveDiscrete(x, y);

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
        moveDiscrete: function(x, y) {
            var dx = Math.floor(x % 10),
                dy = Math.floor(y % 10);

            this.discreteX = x;
            this.discreteY = y;

            if(dx === 0) {
                this.x = Math.floor(x / 10);
            }

            if(dy === 0) {
               this.y = Math.floor(y / 10);
            }
        },
        moveTo: function (x, y) {
            this.discreteX = x * 10;
            this.discreteY = y * 10;
            this.x = x;
            this.y = y;
        }
    };

})(jQuery, window);