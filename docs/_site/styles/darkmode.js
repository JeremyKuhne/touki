(function () {
    console.log("Dark mode script loaded");
    
    const setTheme = theme => {
        const isDark = theme === "dark";
        document.body.classList.toggle("dark", isDark);
        console.log("Theme set to:", theme, "Body has dark class:", document.body.classList.contains("dark"));
    };

    // Check OS preference
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    console.log("OS prefers dark mode:", mq.matches);
    
    // Apply theme based on OS preference
    setTheme(mq.matches ? "dark" : "light");
    
    // Listen for OS theme changes
    mq.addEventListener("change", e => {
        console.log("OS theme changed to:", e.matches ? "dark" : "light");
        setTheme(e.matches ? "dark" : "light");
    });
    
    // Add keyboard shortcut Ctrl+Shift+D to toggle dark mode manually for testing
    document.addEventListener("keydown", e => {
        if (e.ctrlKey && e.shiftKey && e.key === "D") {
            const currentlyDark = document.body.classList.contains("dark");
            setTheme(currentlyDark ? "light" : "dark");
            console.log("Manual toggle to:", currentlyDark ? "light" : "dark");
        }
    });
})();
