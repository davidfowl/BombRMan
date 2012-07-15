(function($, window) {

    window.Game.Bomber = function() {
        this.x = 0;
        this.y = 0;
        this.type = window.Game.Sprites.BOMBER;
        this.order = 2;
        this.maxBombs = 2;
        this.power = 1;
        this.direction = 0;
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
            var x = this.x,
                y = this.y,
                handled = false;

            switch(keyCode) {
                case window.Game.Keys.UP:
                    if(game.movable(x, y - 1)) {
                        y -= 1;
                    }
                    handled = true;
                    break;
                case window.Game.Keys.DOWN:
                    if(game.movable(x, y + 1)) {
                        y += 1;
                    }
                    handled = true;
                    break;
                case window.Game.Keys.LEFT:
                    if(game.movable(x - 1, y)) {
                        x -= 1;
                    }
                    handled = true;
                    break;
                case window.Game.Keys.RIGHT:
                    if(game.movable(x + 1, y)) {
                        x += 1;
                    }
                    handled = true;
                    break;
                case window.Game.Keys.A:
                    this.createBomb(game);
                    handled = true;
                    break;
            }

            this.moveTo(x, y);

            return handled;
        },
        explode: function(game) {
            game.removeSprite(this);
        },
        removeBomb: function() {
            this.bombs--;
        },
        increaseMaxBombs: function() {
            this.maxBombs++;
        },
        increasePower: function() {
            this.power++;
        },
        moveTo: function (x, y) {
            this.x = x;
            this.y = y;
        }
    };

})(jQuery, window);