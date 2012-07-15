(function($, window) {

window.Game.Utils = {
    indexOf: function(arr, value) {
        for(var i = 0; i < arr.length; ++i) {
            if(value === arr[i]) {
                return i;
            }
        }   
        return -1;
    }
};

})(jQuery, window);
