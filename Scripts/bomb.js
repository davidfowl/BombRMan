(function($, window) {
    var dirs = [[1, 0], [0, 1], [-1, 0], [0, -1]];

    window.Game.Bomb = function(x, y, duration, power, bombType, player) {
        this.x = x;
        this.y = y;
        this.ticks = window.Game.TicksPerSecond * duration;
        this.order = 0;
        this.power = power;
        this.player = player;
        this.type = window.Game.Sprites.BOMB;
        this.bombType = bombType;
    };

    window.Game.Bomb.prototype = {
        update: function(game) {
            if(this.ticks > 0) {
                this.ticks--;
                if(this.ticks === 0) {
                    this.explode(game);
                }
            }
        },
        explode: function(game) {
            this.player.removeBomb();
            game.removeSprite(this); 

            // TODO: Add logic to base this on the bomb's power level
            game.addSprite(new window.Game.Explosion(this.x, this.y, 1));
            for(var i = 0; i < dirs.length; ++i) {
                var dx = dirs[i][0],
                    dy = dirs[i][1],
                    x = this.x + dx,
                    y = this.y + dy;

                if(game.canDestroy(x, y)) { 
                    game.addSprite(new window.Game.Explosion(x, y, 1));
                }
            }
        }
    };

})(jQuery, window);