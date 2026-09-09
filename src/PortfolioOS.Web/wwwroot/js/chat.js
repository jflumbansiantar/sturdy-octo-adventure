// Keeps the newest message in view. Blazor has no managed equivalent of scrollTop, and the
// chat log is the one place in this app where the bottom of a container is what matters.
window.portfolioOsChat = {
    scrollToBottom: function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    }
};
