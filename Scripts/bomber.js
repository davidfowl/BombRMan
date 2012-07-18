(function($, window) {
    var DELTA = 10,
        POWER = 100,
        FRAME_RATE = Math.floor(window.Game.TicksPerSecond / 2);

    window.Game.Bomber = function() {
        this.x = 0;
        this.y = 0;
        this.exactX = 0;
        this.exactY = 0;

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

            if(game.inputManager.isKeyUp(window.Game.Keys.UP)) {
                this.directionY = 0;
            }

            if(game.inputManager.isKeyUp(window.Game.Keys.DOWN)) {
                this.directionY = 0;
            }

            if(game.inputManager.isKeyUp(window.Game.Keys.LEFT)) {
                this.directionX = 0;
            }

            if(game.inputManager.isKeyUp(window.Game.Keys.RIGHT)) {
                this.directionX = 0;
            }

            if(game.inputManager.isKeyDown(window.Game.Keys.UP)) {
                this.direction = window.Game.Direction.NORTH;
                this.directionY = -1;
                moving = true;
            }
            
            if(game.inputManager.isKeyDown(window.Game.Keys.DOWN)) {
                this.direction = window.Game.Direction.SOUTH;
                this.directionY = 1;
                moving = true;
            }
            
            if(game.inputManager.isKeyDown(window.Game.Keys.LEFT)) {
                this.direction = window.Game.Direction.WEST;
                this.directionX = -1;
                moving = true;
            }            
            
            if(game.inputManager.isKeyDown(window.Game.Keys.RIGHT)) {
                this.direction = window.Game.Direction.EAST;
                this.directionX = 1;
                moving = true;
            }
            
            if(moving) {
                if(!this.moving) {
                    this.moving = true;
                    this.frameLength = game.assetManager.getMetadata(this).frames[this.direction].length;
                    this.activeFrameIndex = 1;
                    this.movingTicks = 0;
                }
                else {
                    this.movingTicks++;
                    if(this.movingTicks % FRAME_RATE === 0) {
                        this.activeFrameIndex = (this.activeFrameIndex + 1) % this.frameLength;
                    }
                }
            }
            else {
                this.directionY = 0;
                this.directionX = 0;
                this.moving = false;
                this.activeFrameIndex = 0;
            }

            if(game.inputManager.isKeyPress(window.Game.Keys.A) || 
               game.inputManager.isKeyDown(window.Game.Keys.A)) {
                this.createBomb(game);
            }
        },
        update: function(game) {
            var x = this.exactX,
                y = this.exactY;

            this.handleInput(game);

            x += DELTA * this.directionX;
            y += DELTA * this.directionY;

            this.moveExact(game, x, y);

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
        getXHitTargets: function() {
            if(this.directionX === 1) {
                return [{ x: 1, y: -1 }, { x: 1, y: 0 }, { x: 1, y: 1 }];
            }
            else if(this.directionX === -1) {
                return [{ x: -1, y: -1 }, { x: -1, y: 0 }, { x: -1, y: 1 }];
            }
            return [];
        },
        getYHitTargets: function() {
            if(this.directionY === -1) {
                return [{ x: -1, y: -1 }, { x: 0, y: -1 }, { x: 1, y: -1 }];
            }
            else if(this.directionY === 1) {
                return [{ x: -1, y: 1 }, { x: 0, y: 1 }, { x: 1, y: 1 }];
            }

            return [];
        },
        getHitTargets: function() {
            var targets = [],
                xs = this.getXHitTargets();
                ys = this.getYHitTargets();

            for(var i = 0; i < xs.length; ++i) {
                targets.push(xs[i]);
            }

            for(var i = 0; i < ys.length; ++i) {
                targets.push(ys[i]);
            }

            return targets;
        },
        moveExact: function(game, x, y) {
            this.effectiveX = x / POWER;
            this.effectiveY = y / POWER;

            var actualX = Math.floor((x + (POWER / 2)) / POWER),
                actualY = Math.floor((y + (POWER / 2)) / POWER),
                targets = this.getHitTargets(),
                sourceLeft = this.effectiveX * game.map.tileSize,
                sourceTop = this.effectiveY * game.map.tileSize,
                sourceRect = {
                    left: sourceLeft,
                    top: sourceTop,
                    right: sourceLeft + game.map.tileSize,
                    bottom : sourceTop + game.map.tileSize
                };

            window.Game.Logger.clear();
            window.Game.Logger.log('source=' + JSON.stringify(sourceRect));
            window.Game.Logger.log('exactX=' + (this.exactX / POWER) + ', exactY=' + (this.exactY / POWER));
            window.Game.Logger.log('actualX=' + actualX + ', actualY=' + actualY);
            window.Game.Logger.log('directionX=' + this.directionX + ', directionY=' + this.directionY);

            var collisions = [];
            for(var i = 0; i < targets.length; ++i) { 
                var tx = targets[i].x,
                    ty = targets[i].y,
                    left = (actualX + tx) * game.map.tileSize,
                    top = (actualY + ty) * game.map.tileSize,
                    targetRect = {
                        left: left,
                        top: top,
                        right: left + game.map.tileSize,
                        bottom: top + game.map.tileSize
                    },
                    movable = game.movable(Math.floor(left / game.map.tileSize), 
                                           Math.floor(top / game.map.tileSize)),
                    intersects = window.Game.Utils.intersects(sourceRect, targetRect);

                if(!movable && intersects) {
                    collisions.push({ x: actualX + tx, y: actualY + ty });

                    window.Game.Logger.log('collision=(' + (actualX + tx) + ', ' + (actualY + ty) +')');
                }
            }

            switch(collisions.length) {
                case 0:
                this.x = actualX;
                this.y = actualY;

                this.exactX = x;
                this.exactY = y;
                break;
                case 1:
                    var diffY = (collisions[0].y * POWER - this.exactY),
                        diffX = (collisions[0].x * POWER - this.exactX),
                        absX = Math.abs(diffX),
                        absY = Math.abs(diffY);

                    if(absY >= 35 && absY < 100) {
                        this.exactY += DELTA * -window.Game.Utils.sign(diffY);
                    }

                    if(absX >= 35  && absX < 100) {
                        this.exactX += DELTA * -window.Game.Utils.sign(diffX);
                    }

                    window.Game.Logger.log('diffX=(' + absX +', diffY=' + absY + ')');
                break;
            }
        },
        moveTo: function (x, y) {
            this.exactX = x * POWER;
            this.exactY = y * POWER;
            this.effectiveX = x;
            this.effectiveY = y;
            this.x = x;
            this.y = y;
        }
    };

})(jQuery, window);