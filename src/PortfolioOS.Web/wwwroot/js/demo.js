// Warns a test-drive user that closing or reloading the tab is not how a session ends cleanly.
//
// Only a warning: the tab closing does NOT delete the sandbox, because a `pagehide` hook cannot
// tell a reload from a departure and would wipe the data of anyone who pressed F5. The server
// times abandoned sessions out instead; this prompt just makes sure nobody walks away thinking
// their data is safely stored.
window.portfolioDemo = {
    _handler: null,

    armUnloadWarning: function () {
        if (this._handler) return;

        this._handler = function (e) {
            // Browsers ignore custom text now and show their own wording, but both the
            // preventDefault and the returnValue assignment are still needed to trigger it.
            e.preventDefault();
            e.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', this._handler);
    },

    disarmUnloadWarning: function () {
        if (!this._handler) return;

        window.removeEventListener('beforeunload', this._handler);
        this._handler = null;
    }
};
