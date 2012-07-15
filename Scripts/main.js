(function($, window) {

var requestAnimFrame = (function() {
  return window.requestAnimationFrame ||
     window.webkitRequestAnimationFrame ||
     window.mozRequestAnimationFrame ||
     window.oRequestAnimationFrame ||
     window.msRequestAnimationFrame ||
     function(callback, element) {
       window.setTimeout(callback, window.Game.FPS);
     };
})();

$(function() {
    var canvas = document.getElementById('canvas');
    var context = canvas.getContext('2d');
    var engine = new window.Game.Engine();
    var assetManager = new window.Game.AssetManager();
    var renderer = new window.Game.Renderer(assetManager);

    animate(engine, renderer, canvas, context);

    $(document).keydown(function(e) { 
        if(engine.onInput(e)) {
            e.preventDefault();
            return false;
        }
    });
});

function animate(engine, renderer, canvas, context) { 

    engine.update();

    context.clearRect(0, 0, canvas.width, canvas.height);

    renderer.draw(engine, context);

    // request new frame
    requestAnimFrame(function() {
      animate(engine, renderer, canvas, context);
    });
}

})(jQuery, window);