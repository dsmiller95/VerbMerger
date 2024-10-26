// @ts-check
/// <reference types="jquery" />
/// <reference types="interactjs" />

/**
 * @type {Array<HTMLElement>}
 * Stores the selected items for the noun-verb-noun sequence.
 */
let selectedItems = [];


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
 * Returns the text content of the selected items.
 * @returns {string[]}
 */
function selectedText() {
    return selectedItems.map(x => $(x).text());
}

/**
 * Updates the click history display.
 */
function updateHistory() {
    $('#clickHistory').text(selectedText().join(' -> '));
}
function setHistoryResult(extraResult){
    const history = $('#clickHistory');
    const prevText = history.text();
    const newText = prevText + " = " + extraResult;
    history.text(newText);
}

/**
 * Handles node selection and updating the selected items.
 * @param {JQuery<HTMLElement>} $node - The clicked node element.
 */
function selectNode($node) {
    $node.addClass('highlight');
    selectedItems.push($node.get());
    updateHistory();
}

/**
 * Initiates a merge request to the backend API.
 * Clears the selected items and resets the highlighted nodes after completion.
 */
async function initiateMerge() {
    const [subject, verb, object] = selectedText();
    
    const mergeResult = await getMergeResult(subject, verb, object);
    if(mergeResult){
        displayResult(mergeResult.word, mergeResult.partOfSpeech);
    }

    clearSelections();
}

/**
 * Clears the current selections and resets the highlighted nodes.
 * This function can be called to reset the selection state.
 * @returns {void}
 */
function clearSelections() {
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
            partOfSpeech: result.partOfSpeech.toLowerCase()
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

    setHistoryResult(word + ", " + partOfSpeech);
    
    const existing = $('.node').filter((_, node) => $(node).text() === word && $(node).attr('data-type') === partOfSpeech);
    if(existing.length > 0) return; // Prevent duplicates
    
    
    const className = "node " + (partOfSpeech === 'verb' ? 'verb' : 'noun');
    const $node = $('<div class="' + className + '"></div>');
    $node.text(word);
    $node.attr('data-type', partOfSpeech === 'verb' ? 'verb' : 'noun');
    $node.css({
        top: `${Math.random() * 200 + 100}px`,
        left: `${Math.random() * 200 + 100}px`
    });

    // Add click event and draggable functionality to the new node
    $node.on('click', () => {
        //const $node = $(this);
        handleNodeClick($node);
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
        handleNodeClick($node);
    });

    initializeDraggableNodes();
}

/**
 * Handles the click event for a node.  
 * @param {JQuery<HTMLElement>} $node - The clicked node element.
 */
function handleNodeClick($node) {
    const type = $node.attr('data-type');

    if(type === "noun"){
        if (selectedItems.length === 0 || selectedItems.length === 2) {
            selectNode($node);
        }else{
            clearSelections();
        }
    }else if (type === "verb") {
        if (selectedItems.length === 1) {
            selectNode($node);
        }else {
            clearSelections();
        }
    }

    if (selectedItems.length === 3) {
        initiateMerge();
    }
}

// Initialize the nodes on page load
$(document).ready(setupNodes);
