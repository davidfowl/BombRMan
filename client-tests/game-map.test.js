'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { loadClientScripts } = require('./load-client');

test('Game.Map fill/get/set/snapshot round-trips tile values', () => {
    const window = loadClientScripts(['constants.js', 'map.js']);
    const map = new window.Game.Map(3, 2, 32);

    // "fill" parses a packed digit string row-major, as the server sends it.
    map.fill('012345');

    assert.equal(map.get(0, 0), 0);
    assert.equal(map.get(1, 0), 1);
    assert.equal(map.get(2, 0), 2);
    assert.equal(map.get(0, 1), 3);
    assert.equal(map.get(1, 1), 4);
    assert.equal(map.get(2, 1), 5);

    map.set(1, 1, 9);
    assert.equal(map.get(1, 1), 9);

    assert.deepEqual(Array.from(map.snapshot()), [0, 1, 2, 3, 9, 5]);
});

test('Game.Map exposes its configured dimensions', () => {
    const window = loadClientScripts(['constants.js', 'map.js']);
    const map = new window.Game.Map(15, 13, 32);

    assert.equal(map.width, 15);
    assert.equal(map.height, 13);
    assert.equal(map.tileSize, 32);
});
