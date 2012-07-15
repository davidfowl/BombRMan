(function($, window) {
    var TILE_SIZE = 24;

    window.Game.Renderer = function() {
    };

    window.Game.Renderer.prototype = {
        draw : function(game, context) {
            // Draw the map 
            for(var i = 0; i < game.map.width; ++i) {
                for(var j = 0; j < game.map.height; ++j) {
                    switch(game.map.get(i, j)) {
                        case game.types.GRASS:
                            context.fillStyle = '#458B00';
                            break;
                        case game.types.WALL:
                            context.fillStyle = '#bebebe';
                            break;
                        case game.types.BRICK:
                            context.fillStyle = '#cecece';
                            break;
                    }

                    context.fillRect(i * TILE_SIZE, j * TILE_SIZE, TILE_SIZE, TILE_SIZE);
                }
            }
            
            for(var i = 0; i < game.sprites.length; ++i) {
                var sprite = game.sprites[i];
                switch(sprite.type) {
                    case window.Game.Sprites.EXPLOSION:
                        context.fillStyle = 'yellow';
                        context.fillRect(sprite.x * TILE_SIZE, sprite.y * TILE_SIZE, TILE_SIZE, TILE_SIZE);
                        break;
                    case window.Game.Sprites.BOMB:
                        context.fillStyle = '#000';
                        context.beginPath();
                        context.arc(sprite.x * TILE_SIZE + (TILE_SIZE * 0.5), 
                                    sprite.y * TILE_SIZE + (TILE_SIZE * 0.5), 
                                    0.45 * TILE_SIZE, 0, 2 * Math.PI, false);
                        context.fill();
                        break;
                    case window.Game.Sprites.BOMBER:
                        context.fillStyle = '#FFF';
                        context.fillRect(sprite.x * TILE_SIZE, sprite.y * TILE_SIZE, TILE_SIZE, TILE_SIZE);        
                        break;
                }
            }
        }
    };

})(jQuery, window);