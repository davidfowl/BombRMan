(function($, window) {
    window.Game.Explosion = function(x, y, duration) {
        this.x = x;
        this.y = y;
        this.order = 1;
        this.type = window.Game.Sprites.EXPLOSION;
        this.ticks = window.Game.TicksPerSecond * duration;
    };

    window.Game.Explosion.prototype = {
        update: function(game) {
            if(this.ticks > 0) {
                this.ticks--;
                game.onExplosion(this.x, this.y);

                if(this.ticks === 0) {
                    game.onExplosionEnd(this.x, this.y);
                    game.removeSprite(this);
                }
            }
        }
    };

})(jQuery, window);