// @ts-check
/// <reference types="jquery" />
/// <reference types="interactjs" />

/**
 * @type {Array<HTMLElement>}
 * Stores the selected items for the noun-verb-noun sequence.
 */
let selectedItems = [];

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
    
    const loadingNode = createLoadingNode();
    clearSelections();
    
    const mergeResult = await getMergeResult(subject, verb, object);
    if(mergeResult) {
        displayResult(mergeResult.word, mergeResult.partOfSpeech, loadingNode);
    }else{
        $(loadingNode).remove();
    }

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
 * Clears the current selections and resets the highlighted nodes.
 * This function can be called to reset the selection state.
 * @returns {void}
 */
function clearSelections() {
    $('.node.highlight').removeClass('highlight');
    selectedItems = [];
}

/**
 * Creates a new draggable node to display the result from the API.
 * @param {string} word - The word returned from the API.
 * @param {string} partOfSpeech - The part of speech of the returned word, either 'noun' or 'verb'.
 * @param {HTMLElement} loadingNode - The existing loading node
 */
function displayResult(word, partOfSpeech, loadingNode) {
    const existing = $('.node').filter((_, node) => $(node).text() === word && $(node).attr('data-type') === partOfSpeech);
    
    const $node = $(loadingNode);
    
    $node.addClass(partOfSpeech === 'verb' ? 'verb' : 'noun');
    $node.text(word);
    $node.attr('data-type', partOfSpeech === 'verb' ? 'verb' : 'noun');

    // Add click event and draggable functionality to the new node
    $node.on('click', () => {
        //const $node = $(this);
        handleNodeClick($node);
    });

    if(existing.length > 0) {
        // animate the loading node towards the existing node, then clear it
        const $existingNode = $(existing[0]);
        const $loadingNode = $node;
        
        // Calculate the position difference
        const existingOffset = $existingNode.offset();
        const loadingOffset = $loadingNode.offset();
        const deltaX = existingOffset.left - loadingOffset.left;
        const deltaY = existingOffset.top - loadingOffset.top;
        
        // Apply the transition class and animate
        $loadingNode.addClass('transitioning');
        $loadingNode.css('transform', `translate(${deltaX}px, ${deltaY}px)`);

        // Remove the loading node after the animation completes
        $loadingNode.one('transitionend', () => {
            $loadingNode.remove();
        });

        return;
    }
    
    // only allow dragging if the node did not already exist
    interact($node[0]).draggable(defaultDraggableOptions);
}

/**
 * Clears the current selections and resets the highlighted nodes.
 * This function can be called to reset the selection state.
 * @returns {HTMLElement} - The new loading node.
 */
function createLoadingNode(){
    const $node = $('<div class="node"></div>');
    $node.text("Loading...");
    $node.css({
        top: `${Math.random() * 200 + 100}px`,
        left: `${Math.random() * 200 + 100}px`
    });
    $('#workspace').append($node);
    
    return $node.get(0);
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


const defaultDraggableOptions = {
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
};

/**
 * Initializes interact.js for draggable functionality.
 */
function initializeDraggableNodes() {
    interact('.node').draggable(defaultDraggableOptions);
}



// Initialize the nodes on page load
$(document).ready(setupNodes);
