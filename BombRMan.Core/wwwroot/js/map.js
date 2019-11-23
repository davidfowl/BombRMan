(function($, window) {
    var map = [];

    window.Game.Map = function(width, height, tileSize) {
        this.width = width;
        this.height = height;
        this.tileSize = tileSize;

        this.getIndex = function (x, y) {
            return (y  * this.width) + x;
        }

        for(var i = 0; i < width * height; ++i) {
            map[i] = 0;
        }
    };

    window.Game.Map.prototype = {
        set: function(x, y, value) {
            map[this.getIndex(x, y)] = value;
        },
        get: function(x, y) {
            return map[this.getIndex(x, y)];
        },
        fill : function(mapValue) {
            for(var i = 0; i < mapValue.length; ++i) {
                map[i] = mapValue.charAt(i) - '0';
            }
        }
    };

})(jQuery, window);