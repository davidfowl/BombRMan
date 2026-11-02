'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { loadClientScripts } = require('./load-client');

function makeUtils() {
    // utils.js touches `window.Game.Debugging`, which constants.js defines.
    const window = loadClientScripts(['constants.js', 'utils.js']);
    return window.Game.Utils;
}

test('Utils.indexOf finds an existing element and returns -1 when absent', () => {
    const Utils = makeUtils();
    const arr = ['a', 'b', 'c'];

    assert.equal(Utils.indexOf(arr, 'b'), 1);
    assert.equal(Utils.indexOf(arr, 'z'), -1);
});

test('Utils.sign returns -1/0/1 for negative/zero/positive input', () => {
    const Utils = makeUtils();

    assert.equal(Utils.sign(-42), -1);
    assert.equal(Utils.sign(0), 0);
    assert.equal(Utils.sign(42), 1);
});

test('Utils.intersects detects overlapping and non-overlapping rectangles', () => {
    const Utils = makeUtils();
    const r1 = { left: 0, top: 0, right: 10, bottom: 10 };
    const overlapping = { left: 5, top: 5, right: 15, bottom: 15 };
    const touchingEdge = { left: 10, top: 0, right: 20, bottom: 10 };
    const disjoint = { left: 20, top: 20, right: 30, bottom: 30 };

    assert.equal(Utils.intersects(r1, overlapping), true);
    // Edge-touching rectangles are not considered intersecting (strict <, >).
    assert.equal(Utils.intersects(r1, touchingEdge), false);
    assert.equal(Utils.intersects(r1, disjoint), false);
});

test('Logger.log/clear are no-ops without throwing when there is no DOM and debugging is off', () => {
    const window = loadClientScripts(['constants.js', 'utils.js']);

    assert.doesNotThrow(() => window.Game.Logger.log('hello'));
    assert.doesNotThrow(() => window.Game.Logger.clear());
});
