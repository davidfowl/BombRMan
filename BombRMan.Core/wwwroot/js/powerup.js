(function($, window) {
    window.Game.Powerup = function(x, y, duration, powerupType) {
        this.x = x;
        this.y = y;
        this.ticks = window.Game.TicksPerSecond * duration;
        this.order = 1;
        this.type = window.Game.Sprites.POWERUP;
        this.powerupType = powerupType;
    };

    window.Game.Powerup.prototype = {
        update: function(game) {
            if(this.ticks > 0) {
                this.ticks--;
                if(this.ticks === 0) {
                    this.explode(game);
                }
            }
        },
        explode: function(game) {
            game.removeSprite(this);
        }
    };

})(jQuery, window);