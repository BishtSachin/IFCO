// In wwwroot/js/site.js
function getSelectedValues(selectElement) {
    // A quick check to prevent errors if the element doesn't exist
    if (!selectElement) {
        return [];
    }

    // Use modern array methods for a clean, single line of code
    return Array.from(selectElement.selectedOptions).map(option => option.value);
}