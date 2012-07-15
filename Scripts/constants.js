(function($, window) {

window.Game = {};

window.Game.Sprites = {
    BOMB : 0,
    EXPLOSION : 1,
    BOMBER : 2
};

window.Game.Keys = {
    UP : 38,
    DOWN : 40,
    LEFT : 37,
    RIGHT : 39,
    A : 65
};

window.Game.FPS = 1000 / 60;
window.Game.TicksPerSecond = 30;


})(jQuery, window);