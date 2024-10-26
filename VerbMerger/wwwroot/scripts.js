/**
 * Handles the form submission event.
 * @param {Event} event - The form submission event.
 */
document.getElementById('mergeForm').addEventListener('submit', async function(event) {
    event.preventDefault();

    /** @type {string} */
    const subject = document.getElementById('subject').value;
    /** @type {string} */
    const verb = document.getElementById('verb').value;
    /** @type {string} */
    const object = document.getElementById('object').value;

    /** @type {Response} */
    const response = await fetch(`/merge?subject=${subject}&verb=${verb}&object=${object}`);
    /** @type {Object} */
    const result = await response.json();

    document.getElementById('result').innerText = JSON.stringify(result, null, 2);
});