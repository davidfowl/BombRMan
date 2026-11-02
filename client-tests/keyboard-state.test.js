'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { loadClientScripts } = require('./load-client');

// Stub SignalR so Game.Engine's constructor (which builds a hub connection)
// doesn't need a real network stack. We never call initialize()/start(), so
// none of these are actually invoked in these tests - they just need to exist.
function stubSignalR() {
    return {
        HubConnectionBuilder: function HubConnectionBuilder() {
            return {
                withUrl: function () { return this; },
                build: function () {
                    return {
                        state: 'Disconnected',
                        on: function () {},
                        start: function () { return Promise.resolve(); },
                        send: function () { return Promise.resolve(); }
                    };
                }
            };
        }
    };
}

function makeEngine() {
    const window = loadClientScripts(
        ['constants.js', 'utils.js', 'map.js', 'game.js'],
        { window: { signalR: stubSignalR() } }
    );

    const engine = new window.Game.Engine({});
    return { window, engine };
}

test('key press is tracked by the bitfield-backed input manager', () => {
    const { window, engine } = makeEngine();
    const UP = window.Game.Keys.UP;

    assert.equal(engine.inputManager.isKeyDown(UP), false);
    assert.equal(engine.inputManager.isKeyUp(UP), true);

    engine.onKeydown({ keyCode: UP });

    assert.equal(engine.inputManager.isKeyDown(UP), true);
    assert.equal(engine.inputManager.isKeyUp(UP), false);
});

test('key release clears the bit without affecting other keys', () => {
    const { window, engine } = makeEngine();
    const UP = window.Game.Keys.UP;
    const LEFT = window.Game.Keys.LEFT;

    engine.onKeydown({ keyCode: UP });
    engine.onKeydown({ keyCode: LEFT });

    assert.equal(engine.inputManager.isKeyDown(UP), true);
    assert.equal(engine.inputManager.isKeyDown(LEFT), true);

    engine.onKeyup({ keyCode: UP });

    assert.equal(engine.inputManager.isKeyDown(UP), false);
    assert.equal(engine.inputManager.isKeyDown(LEFT), true);
});

test('isKeyPress/isHoldingKey reflect the transition between prev/current tick state', () => {
    const { window, engine } = makeEngine();
    engine.sprites = [];
    const UP = window.Game.Keys.UP;

    engine.onKeydown({ keyCode: UP });
    assert.equal(engine.inputManager.isKeyPress(UP), true, 'first tick should be a fresh press');
    assert.equal(engine.inputManager.isHoldingKey(UP), false, 'not held yet - prevKeyState has not rotated');

    // Engine.update() rotates keyState into prevKeyState at the end of each tick.
    engine.update();

    assert.equal(engine.inputManager.isKeyPress(UP), false, 'no longer a fresh press on the next tick');
    assert.equal(engine.inputManager.isHoldingKey(UP), true, 'held once prevKeyState has rotated in');
});
