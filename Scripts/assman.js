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