(function ($, window) {
    var SCALE_FACTOR = 1.5,
        BASE_TILE_SIZE = 24;

    window.Game.Renderer = function (assetManager) {
        this.assetManager = assetManager;
        this.ticks = 0;
    };

    window.Game.Renderer.prototype = {
        draw: function (game, context) {
            this.ticks++;

            // Draw the map 
            for (var i = 0; i < game.map.width; ++i) {
                for (var j = 0; j < game.map.height; ++j) {
                    switch (game.map.get(i, j)) {
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

                    context.fillRect(i * game.map.tileSize, j * game.map.tileSize, game.map.tileSize, game.map.tileSize);
                }
            }

            for (var i = 0; i < game.sprites.length; ++i) {
                var sprite = game.sprites[i];
                switch (sprite.type) {
                    case window.Game.Sprites.EXPLOSION:
                        context.fillStyle = 'yellow';
                        context.fillRect(sprite.x * game.map.tileSize, sprite.y * game.map.tileSize, game.map.tileSize, game.map.tileSize);
                        break;
                    case window.Game.Sprites.BOMB:
                        context.fillStyle = '#000';
                        context.beginPath();
                        context.arc(sprite.x * game.map.tileSize + (game.map.tileSize * 0.5),
                                    sprite.y * game.map.tileSize + (game.map.tileSize * 0.5),
                                    0.45 * game.map.tileSize, 0, 2 * Math.PI, false);
                        context.fill();
                        break;
                    case window.Game.Sprites.BOMBER:
                        var metadata = this.assetManager.getMetadata(sprite);
                        frame = metadata.frames[sprite.direction][sprite.activeFrameIndex],
                            x = sprite.exactX / 100,
                            y = sprite.exactY / 100,
                            scale = (game.map.tileSize / BASE_TILE_SIZE) * SCALE_FACTOR,
                            width = metadata.width * scale,
                            height = metadata.height * scale;


                        // Bounding Box 
                        /*if (window.Game.Debugging) {
                            context.fillStyle = 'orange';
                            context.fillRect(sprite.x * game.map.tileSize, sprite.y * game.map.tileSize, game.map.tileSize, game.map.tileSize);

                            context.fillStyle = 'purple';
                            context.fillRect(x * game.map.tileSize, y * game.map.tileSize, game.map.tileSize, game.map.tileSize);

                            var targets = sprite.getHitTargets();
                            context.fillStyle = 'red';
                            for (var j = 0; j < targets.length; ++j) {
                                var xx = sprite.x + targets[j].x,
                                    yy = sprite.y + targets[j].y;
                                context.fillRect(xx * game.map.tileSize, yy * game.map.tileSize, game.map.tileSize, game.map.tileSize);
                            }

                            if (sprite.candidate) {
                                context.fillStyle = 'yellow';
                                context.fillRect(sprite.candidate.x * game.map.tileSize, sprite.candidate.y * game.map.tileSize, game.map.tileSize, game.map.tileSize);
                            }
                        }*/

                        if (sprite.transparent) {
                            context.globalAlpha = 0.5;
                        }

                        context.drawImage(metadata.image,
                                          frame.x,
                                          frame.y,
                                          metadata.width,
                                          metadata.height,
                                          x * game.map.tileSize,
                                          (y * game.map.tileSize) - (0.9 * game.map.tileSize),
                                          width,
                                          height);

                        context.globalAlpha = 1.0;
                        break;
                    case window.Game.Sprites.POWERUP:
                        context.fillStyle = 'orange';
                        context.fillRect(sprite.x * game.map.tileSize, sprite.y * game.map.tileSize, game.map.tileSize, game.map.tileSize);
                        break;
                }
            }
        }
    };

})(jQuery, window);