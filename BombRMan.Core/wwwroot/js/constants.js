(function($, window) {

window.Game = {};

window.Game.Sprites = {
    BOMB : 0,
    EXPLOSION : 1,
    BOMBER : 2,
    POWERUP : 3
};

window.Game.Keys = {
    UP : 38,
    DOWN : 40,
    LEFT : 37,
    RIGHT : 39,
    A : 65,
    D: 68,
    P: 80
};

window.Game.Bombs = {
    NORMAL: 0,
    BOUNCY : 1,
    REMOTE : 2,
    P : 3
};

window.Game.Powerups = {
    SPEED : 0,
    BOMB : 1,
    EXPLOSION: 2
};

window.Game.Direction = {
    NORTH: 0,
    SOUTH: 1,
    EAST: 2,
    WEST: 3
};

window.Game.TicksPerSecond = 60;
window.Game.Debugging = false;
window.Game.MoveSprites = true;


})(jQuery, window);