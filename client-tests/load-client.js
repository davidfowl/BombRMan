'use strict';

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { performance } = require('node:perf_hooks');

const WWWROOT_JS = path.join(__dirname, '..', 'BombRMan.Core', 'wwwroot', 'js');

/**
 * Loads a subset of the real client scripts (unmodified) into a fresh,
 * isolated sandbox and returns the sandbox's `window` object.
 *
 * This lets tests exercise the actual files shipped to the browser instead of
 * a reimplementation, without needing a browser, bundler, or jQuery/SignalR.
 *
 * The client scripts rely on a few browser globals (`document`, `performance`)
 * directly rather than via `window.*`, so sensible no-op defaults are seeded
 * into the sandbox; pass `extraGlobals` to override them.
 *
 * @param {string[]} files Script filenames (relative to wwwroot/js), in load order.
 * @param {object} [extraGlobals] Extra/overriding top-level sandbox globals
 *   (e.g. a stub `signalR` so game.js's Engine constructor doesn't blow up).
 */
function loadClientScripts(files, extraGlobals) {
    const window = {};
    const defaults = {
        window,
        console,
        performance,
        document: {
            getElementById: () => null,
            addEventListener: () => {}
        }
    };
    const sandbox = Object.assign(defaults, extraGlobals);
    sandbox.window = Object.assign(window, sandbox.window);
    vm.createContext(sandbox);

    for (const file of files) {
        const fullPath = path.join(WWWROOT_JS, file);
        const source = fs.readFileSync(fullPath, 'utf8');
        vm.runInContext(source, sandbox, { filename: fullPath });
    }

    return sandbox.window;
}

module.exports = { loadClientScripts, WWWROOT_JS };
