(function($, window) {
    var DELTA = 10,
        POWER = 100,
        FRAME_RATE = Math.floor(window.Game.TicksPerSecond / 2);

    window.Game.Bomber = function() {
        this.x = 0;
        this.y = 0;
        this.discreteX = 0;
        this.discreteY = 0;

        // Debugging
        this.effectiveX = 0;
        this.effectiveY = 0;

        this.type = window.Game.Sprites.BOMBER;
        this.order = 2;
        this.maxBombs = 1;
        this.power = 1;
        this.speed = 1;
        this.directionX = 0;
        this.directionY = 0;
        
        this.moving = false;
        this.activeFrameIndex = 0;

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
        handleInput: function(game) {
            var moving = false;

            if(game.inputManager.isKeyDown(window.Game.Keys.UP)) {
                this.direction = window.Game.Direction.NORTH;
                this.directionY = -1;
                this.directionX = 0;
                moving = true;
            }
            else if(game.inputManager.isKeyDown(window.Game.Keys.DOWN)) {
                this.direction = window.Game.Direction.SOUTH;
                this.directionY = 1;
                this.directionX = 0;
                moving = true;
            }
            else if(game.inputManager.isKeyDown(window.Game.Keys.LEFT)) {
                this.direction = window.Game.Direction.WEST;
                this.directionY = 0;
                this.directionX = -1;
                moving = true;
            }
            else if(game.inputManager.isKeyDown(window.Game.Keys.RIGHT)) {
                this.direction = window.Game.Direction.EAST;
                this.directionY = 0;
                this.directionX = 1;
                moving = true;
            }
            else {
                this.directionY = 0;
                this.directionX = 0;
                this.moving = false;
                this.activeFrameIndex = 0;
            }

            if(moving) {
                if(!this.moving) {
                    this.moving = true;
                    this.frameLength = game.assetManager.getMetadata(this).frames[this.direction].length;
                    this.movingTicks = 0;
                }
                else {
                    this.movingTicks++;
                    if(this.movingTicks % FRAME_RATE === 0) {
                        this.activeFrameIndex = (this.activeFrameIndex + 1) % this.frameLength;
                    }
                }
            }

            if(game.inputManager.isKeyPress(window.Game.Keys.A)) {
                this.createBomb(game);
            }
        },
        update: function(game) {
            var x = this.discreteX,
                y = this.discreteY;

            this.handleInput(game);

            x += DELTA * this.directionX;
            y += DELTA * this.directionY;

            this.moveDiscrete(game, x, y);

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
            
            this.effectiveX = effectiveX;
            this.effectiveY = effectiveY;

            if(game.movable(effectiveX, effectiveY)) {

                this.y = Math.floor((y + DELTA) / POWER);
                this.x = Math.floor((x + DELTA) / POWER);

                this.discreteX = x;
                this.discreteY = y;
            }

            /*console.log('x=' + this.x + 
                        ', y=' + this.y + 
                        ', discreteX=' + (this.discreteX / POWER) + 
                        ', discreteY=' + (this.discreteY / POWER) +
                        ', effectiveX=' + effectiveX +
                        ', effectiveY=' + effectiveY);*/
        },
        moveTo: function (x, y) {
            this.discreteX = x * POWER;
            this.discreteY = y * POWER;
            this.effectiveX = x;
            this.effectiveY = y;
            this.x = x;
            this.y = y;
        }
    };

})(jQuery, window);