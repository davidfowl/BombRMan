(function ($, window) {

    var requestAnimFrame = (function () {
        // Easier for debugging can change the fps dynamically
        //        return window.requestAnimationFrame ||
        //     window.webkitRequestAnimationFrame ||
        //     window.mozRequestAnimationFrame ||
        //     window.oRequestAnimationFrame ||
        //     window.msRequestAnimationFrame ||
        return function (callback, element) {
            window.setTimeout(callback, 1000 / window.Game.TicksPerSecond);
        };
    })();

    $(function () {
        var canvas = document.getElementById('canvas');
        var context = canvas.getContext('2d');
        var assetManager = new window.Game.AssetManager();
        var engine = new window.Game.Engine(assetManager);
        var renderer = new window.Game.Renderer(assetManager);

        engine.initialize();

        animate(engine, renderer, canvas, context);

        $(document).keydown(function (e) {
            if (engine.onKeydown(e)) {
                e.preventDefault();
                return false;
            }
        });

        $(document).keyup(function (e) {
            if (engine.onKeyup(e)) {
                e.preventDefault();
                return false;
            }
        });
    });

    function animate(engine, renderer, canvas, context) {
        window.Game.Logger.clear();
        window.Game.Logger.log('FPS = ' + window.Game.TicksPerSecond);

        engine.update();

        context.clearRect(0, 0, canvas.width, canvas.height);

        renderer.draw(engine, context);

        // request new frame
        requestAnimFrame(function () {
            animate(engine, renderer, canvas, context);
        });
    }

})(jQuery, window);