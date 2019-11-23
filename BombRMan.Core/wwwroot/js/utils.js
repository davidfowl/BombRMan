(function($, window) {

window.Game.Utils = {
    indexOf: function(arr, value) {
        for(var i = 0; i < arr.length; ++i) {
            if(value === arr[i]) {
                return i;
            }
        }   
        return -1;
    },
    intersects: function(r1, r2) {
        return !(r2.left >= r1.right || 
                 r2.right <= r1.left || 
                 r2.top >= r1.bottom ||
                 r2.bottom <= r1.top);
    },
    sign: function(value) {
        if(value > 0) {
            return 1;
        }
        else if(value < 0) { 
            return -1;
        }
        return 0;
    }
};

window.Game.Logger = {
    log : function(value) {
        if(window.Game.Debugging) {
            $('#debug').append(value);
            $('#debug').append('<br/>');
        }
    },
    clear: function() {
        $('#debug').empty();
    }
};

})(jQuery, window);
