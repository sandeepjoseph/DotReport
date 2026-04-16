/**
 * app-interop.js
 * Global JS utilities called by Blazor C# code via IJSRuntime.
 * Handles: file download, drag-drop, theme hot-swap, PDF save.
 */

window.dotreport = {

    /**
     * Triggers a browser file download.
     * Called from ReportGenerator after PDF bytes are produced.
     */
    downloadFile(fileName, mimeType, data) {
        const blob = new Blob([new Uint8Array(data)], { type: mimeType });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    /**
     * Swaps the active theme CSS sheet and updates the root class.
     */
    applyTheme(themeClass) {
        const root  = document.getElementById('ec-root');
        const sheet = document.getElementById('theme-sheet');

        if (root) {
            root.classList.remove('ec-theme--dark', 'ec-theme--light');
            root.classList.add(themeClass);
        }
        if (sheet) {
            sheet.href = themeClass.includes('dark')
                ? 'css/theme-dark.css'
                : 'css/theme-light.css';
        }
    },

    /**
     * Scrolls the token output container to the bottom as tokens stream in.
     */
    scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    },

    /**
     * Returns the viewport dimensions.
     */
    getViewport() {
        return { width: window.innerWidth, height: window.innerHeight };
    }
};
