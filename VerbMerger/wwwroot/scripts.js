// @ts-check
/// <reference types="interactjs" />

/**
 * @type {Array<string>}
 * Stores the selected items for the noun-verb-noun sequence.
 */
let selectedItems = [];

/**
 * @type {Array<string>}
 * Stores the history of clicks for display.
 */
let clickHistory = [];

/**
 * Initializes interact.js for draggable functionality.
 */
function initializeDraggableNodes() {
    interact('.node').draggable({
        listeners: {
            move(event) {
                const $target = $(event.target);
                const x = (parseFloat($target.attr('data-x')) || 0) + event.dx;
                const y = (parseFloat($target.attr('data-y')) || 0) + event.dy;

                $target.css('transform', `translate(${x}px, ${y}px)`);
                $target.attr('data-x', x);
                $target.attr('data-y', y);
            }
        }
    });
}

/**
 * Updates the click history display.
 */
function updateHistory() {
    $('#clickHistory').text(clickHistory.join(' -> '));
}

/**
 * Handles node selection and updating the selected items.
 * @param {JQuery<HTMLElement>} $node - The clicked node element.
 */
function selectNode($node) {
    $node.addClass('highlight');
    selectedItems.push($node.text());
    clickHistory.push($node.text());
    updateHistory();
}

/**
 * Initiates a merge request to the backend API.
 * Clears the selected items and resets the highlighted nodes after completion.
 */
async function initiateMerge() {
    const [subject, verb, object] = selectedItems;
    
    const mergeResult = await getMergeResult(subject, verb, object);
    if(mergeResult){
        displayResult(mergeResult.word, mergeResult.partOfSpeech);
    }

    // Clear selections after request
    $('.node.highlight').removeClass('highlight');
    selectedItems = [];
}

/**
 * Fetches the merge result from the API.
 * @param {string} subject - The subject noun.
 * @param {string} verb - The verb.
 * @param {string} object - The object noun.
 * @returns {Promise<{word: string, partOfSpeech: string}|null>} - The merged word and its part of speech.
 */
async function getMergeResult(subject, verb, object){
    try {
        const queryParams = new URLSearchParams({ subject, verb, object });
        const response = await fetch('/api/merge?' + queryParams.toString(), {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        const result = await response.json();
        return {
            word: result.word,
            partOfSpeech: result.partOfSpeech
        };
    } catch (error) {
        console.error('Error:', error);
        return null;
    }
}

/**
 * Creates a new draggable node to display the result from the API.
 * @param {string} word - The word returned from the API.
 * @param {string} partOfSpeech - The part of speech of the returned word, either 'noun' or 'verb'.
 */
function displayResult(word, partOfSpeech) {
    const $node = $('<div class="node"></div>');
    $node.text(word);
    $node.attr('data-type', partOfSpeech === 'verb' ? 'verb' : 'noun');
    $node.css({
        top: `${Math.random() * 1500 + 100}px`,
        left: `${Math.random() * 1500 + 100}px`
    });

    // Add click event and draggable functionality to the new node
    $node.on('click', () => {
        const type = $node.attr('data-type');
        if ((selectedItems.length === 0 || selectedItems.length === 2) && type === 'noun') {
            selectNode($node);
        } else if (selectedItems.length === 1 && type === 'verb') {
            selectNode($node);
        }
        if (selectedItems.length === 3) {
            initiateMerge();
        }
    });

    $('#workspace').append($node);
    interact($node[0]).draggable({
        listeners: {
            move(event) {
                const $target = $(event.target);
                const x = (parseFloat($target.attr('data-x')) || 0) + event.dx;
                const y = (parseFloat($target.attr('data-y')) || 0) + event.dy;

                $target.css('transform', `translate(${x}px, ${y}px)`);
                $target.attr('data-x', x);
                $target.attr('data-y', y);
            }
        }
    });
}

/**
 * Sets up event listeners for all nodes and initializes them as draggable.
 */
function setupNodes() {
    $('.node').on('click', function () {
        const $node = $(this);
        const type = $node.attr('data-type');

        if ((selectedItems.length === 0 || selectedItems.length === 2) && type === 'noun') {
            selectNode($node);
        } else if (selectedItems.length === 1 && type === 'verb') {
            selectNode($node);
        }

        if (selectedItems.length === 3) {
            initiateMerge();
        }
    });

    initializeDraggableNodes();
}

// Initialize the nodes on page load
$(document).ready(setupNodes);
