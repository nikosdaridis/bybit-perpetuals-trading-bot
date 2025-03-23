function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(() => {
    }).catch(error => {
        console.error("Failed to copy text: ", error);
    });
}
