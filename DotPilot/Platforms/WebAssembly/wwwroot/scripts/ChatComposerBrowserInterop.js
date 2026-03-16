const nativeTextboxSelector = 'textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"]';
const textboxSelector = `${nativeTextboxSelector}, [role="textbox"]`;
const state = {
    inputAutomationId: '',
    sendButtonAutomationId: '',
    behavior: 'EnterSends',
    attachedInput: null,
    lastModifier: '',
    lastAction: '',
    lastBeforeValue: '',
    lastAfterValue: '',
};

const escapeAttribute = value =>
    `${value ?? ''}`
        .replaceAll('\\', '\\\\')
        .replaceAll('"', '\\"');

const selectorFor = automationId =>
    `[xamlautomationid="${escapeAttribute(automationId)}"], [aria-label="${escapeAttribute(automationId)}"]`;

const resolveHost = automationId =>
    automationId
        ? document.querySelector(selectorFor(automationId))
        : null;

const resolveInput = automationId => {
    const host = resolveHost(automationId);
    if (!host) {
        return null;
    }

    const nativeInput = host.matches(nativeTextboxSelector)
        ? host
        : host.querySelector(nativeTextboxSelector);
    if (nativeInput) {
        return nativeInput;
    }

    return host.matches('[role="textbox"]')
        ? host
        : host.querySelector('[role="textbox"]');
};

const resolveSendButton = automationId => {
    const host = resolveHost(automationId);
    if (!host) {
        return null;
    }

    const clickableSelector = 'button, [role="button"], input[type="button"], input[type="submit"], input[type="checkbox"], input[type="radio"], a[href]';
    return host.matches(clickableSelector)
        ? host
        : host.closest(clickableSelector) ?? host.querySelector(clickableSelector) ?? host;
};

const notifyInputChanged = input => {
    input.dispatchEvent(new Event('input', { bubbles: true, cancelable: true, composed: true }));
    input.dispatchEvent(new Event('change', { bubbles: true, cancelable: true, composed: true }));
};

const moveCaretToEnd = input => {
    if (!input) {
        return;
    }

    if (typeof input.focus === 'function') {
        input.focus({ preventScroll: true });
    }

    if (typeof input.setSelectionRange === 'function') {
        const value = `${input.value ?? ''}`;
        input.setSelectionRange(value.length, value.length);
        return;
    }

    if (!input.isContentEditable) {
        return;
    }

    const selection = globalThis.getSelection?.();
    if (!selection) {
        return;
    }

    const range = document.createRange();
    range.selectNodeContents(input);
    range.collapse(false);
    selection.removeAllRanges();
    selection.addRange(range);
};

const insertNewLine = input => {
    if (!input) {
        return;
    }

    if ('value' in input) {
        const value = `${input.value ?? ''}`;
        const start = typeof input.selectionStart === 'number'
            ? input.selectionStart
            : value.length;
        const end = typeof input.selectionEnd === 'number'
            ? input.selectionEnd
            : start;
        const nextValue = `${value.slice(0, start)}\n${value.slice(end)}`;
        input.value = nextValue;

        if (typeof input.setSelectionRange === 'function') {
            const caret = start + 1;
            input.setSelectionRange(caret, caret);
        }
    } else if (typeof input.setRangeText === 'function') {
        const start = input.selectionStart ?? (input.value?.length ?? 0);
        const end = input.selectionEnd ?? start;
        input.setRangeText('\n', start, end, 'end');
    } else if ('textContent' in input) {
        const value = `${input.textContent ?? ''}`;
        input.textContent = `${value}\n`;
    } else if (input.isContentEditable) {
        document.execCommand('insertLineBreak');
    }

    notifyInputChanged(input);
};

let dotPilotExportsPromise = null;

const getDotPilotExports = () =>
    dotPilotExportsPromise ??= globalThis
        .getDotnetRuntime(0)
        .getAssemblyExports('DotPilot.dll');

const submitMessage = inputAutomationId => {
    void getDotPilotExports()
        .then(exports => exports.DotPilot.Presentation.Controls.ChatComposerBrowserExports.SubmitMessage(inputAutomationId));
};

const requestTextSync = (inputAutomationId, input) => {
    const value = 'value' in input
        ? `${input.value ?? ''}`
        : `${input.textContent ?? ''}`;
    const selectionStart = typeof input.selectionStart === 'number'
        ? input.selectionStart
        : value.length;

    void getDotPilotExports()
        .then(exports => exports.DotPilot.Presentation.Controls.ChatComposerBrowserExports.ApplyText(
            inputAutomationId,
            value,
            selectionStart));
};

const normalizeModifier = modifier => `${modifier ?? ''}`.toLowerCase();

const buildEnterEventInit = modifier => {
    const normalizedModifier = normalizeModifier(modifier);
    return {
        key: 'Enter',
        code: 'Enter',
        which: 13,
        keyCode: 13,
        charCode: 13,
        bubbles: true,
        cancelable: true,
        composed: true,
        shiftKey: normalizedModifier === 'shift',
        ctrlKey: normalizedModifier === 'control' || normalizedModifier === 'ctrl',
        altKey: normalizedModifier === 'alt',
        metaKey: normalizedModifier === 'command' || normalizedModifier === 'meta' || normalizedModifier === 'windows',
    };
};

const shouldSend = hasModifier =>
    state.behavior === 'EnterInsertsNewLine'
        ? hasModifier
        : !hasModifier;

const onInputKeyDown = event => {
    if (event.key !== 'Enter') {
        return;
    }

    const hasModifier = event.shiftKey || event.ctrlKey || event.altKey || event.metaKey;
    const send = shouldSend(hasModifier);
    const input = resolveInput(state.inputAutomationId) ?? event.currentTarget;
    const beforeValue = 'value' in input
        ? `${input.value ?? ''}`
        : `${input.textContent ?? ''}`;
    state.lastModifier = hasModifier
        ? [
            event.shiftKey ? 'shift' : '',
            event.ctrlKey ? 'ctrl' : '',
            event.altKey ? 'alt' : '',
            event.metaKey ? 'meta' : '',
        ].filter(Boolean).join('+')
        : 'none';
    state.lastAction = send ? 'send' : 'newline';
    state.lastBeforeValue = beforeValue;

    event.preventDefault();
    event.stopPropagation();

    if (send) {
        submitMessage(state.inputAutomationId);
        state.lastAfterValue = beforeValue;
        return;
    }

    insertNewLine(input);
    state.lastAfterValue = 'value' in input
        ? `${input.value ?? ''}`
        : `${input.textContent ?? ''}`;

    if (state.inputAutomationId) {
        requestTextSync(state.inputAutomationId, input);
    }
};

const synchronizeInputBinding = () => {
    const nextInput = resolveInput(state.inputAutomationId);
    if (state.attachedInput === nextInput) {
        return;
    }

    if (state.attachedInput) {
        state.attachedInput.removeEventListener('keydown', onInputKeyDown, true);
    }

    state.attachedInput = nextInput;
    if (state.attachedInput) {
        state.attachedInput.addEventListener('keydown', onInputKeyDown, true);
    }
};

const observer = new MutationObserver(() => synchronizeInputBinding());
observer.observe(document.documentElement, { childList: true, subtree: true });

export function synchronize(inputAutomationId, sendButtonAutomationId, behavior) {
    state.inputAutomationId = inputAutomationId;
    state.sendButtonAutomationId = sendButtonAutomationId;
    state.behavior = behavior;
    synchronizeInputBinding();
}

export function dispose(inputAutomationId) {
    if (state.inputAutomationId !== inputAutomationId) {
        return;
    }

    state.inputAutomationId = '';
    state.sendButtonAutomationId = '';
    state.behavior = 'EnterSends';
    synchronizeInputBinding();
}

globalThis.dotPilotComposerInterop = {
    dispatchEnter: (inputAutomationId, modifier) => {
        const input = resolveInput(inputAutomationId);
        if (!input) {
            return false;
        }

        moveCaretToEnd(input);
        const eventInit = buildEnterEventInit(modifier);
        input.dispatchEvent(new KeyboardEvent('keydown', eventInit));
        input.dispatchEvent(new KeyboardEvent('keyup', eventInit));
        return true;
    },
    getDebugState: () => ({
        inputAutomationId: state.inputAutomationId,
        sendButtonAutomationId: state.sendButtonAutomationId,
        behavior: state.behavior,
        hasAttachedInput: !!state.attachedInput,
        attachedInputTag: state.attachedInput?.tagName ?? '',
        hasExportsPromise: !!dotPilotExportsPromise,
        lastModifier: state.lastModifier,
        lastAction: state.lastAction,
        lastBeforeValue: state.lastBeforeValue,
        lastAfterValue: state.lastAfterValue,
    }),
};
