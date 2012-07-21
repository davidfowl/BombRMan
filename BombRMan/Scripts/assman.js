(function($, window) {

    window.Game.AssetManager = function() {
        var bomberImg = new Image();
        bomberImg.src = "data:image/png;base64,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";
        this.metadata = {};

        this.metadata[window.Game.Sprites.BOMBER] = { 
            image : bomberImg, 
            width: 17, 
            height: 25,
            frames : {}
        };

        this.metadata[window.Game.Sprites.BOMBER].frames[window.Game.Direction.NORTH] = [{x: 2, y: 0}, {x: 19, y: 0}, {x: 36, y: 0}];
        this.metadata[window.Game.Sprites.BOMBER].frames[window.Game.Direction.EAST] = [{x: 56, y: 0}, {x: 74, y: 0}, {x: 92, y: 0}];
        this.metadata[window.Game.Sprites.BOMBER].frames[window.Game.Direction.SOUTH] = [{x: 112, y: 0}, {x: 129, y: 0}, {x: 146, y: 0}];
        this.metadata[window.Game.Sprites.BOMBER].frames[window.Game.Direction.WEST] = [{x: 166, y: 0}, {x: 185, y: 0}, {x: 203, y: 0}];
    };

    window.Game.AssetManager.prototype = {
        getMetadata: function(sprite) {
            return this.metadata[sprite.type];
        }
    };

})(jQuery, window);