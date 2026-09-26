(() => {
    const page = document.getElementById('dashboardMain');
    const sidebar = document.getElementById('dashboardSidebar');
    const menu = document.getElementById('menuToggleBtn');
    const backdrop = document.getElementById('sidebarBackdrop');
    const mobile = window.matchMedia('(max-width: 900px)');
    const closeMobile = () => {
        sidebar.classList.remove('is-open');
        backdrop.classList.remove('is-visible');
        menu.setAttribute('aria-expanded', 'false');
    };
    const updateExpanded = () => menu.setAttribute('aria-expanded', String(mobile.matches ? sidebar.classList.contains('is-open') : !sidebar.classList.contains('is-hidden')));
    menu.addEventListener('click', () => {
        if (mobile.matches) {
            sidebar.classList.toggle('is-open');
            backdrop.classList.toggle('is-visible');
        } else {
            sidebar.classList.toggle('is-hidden');
            page.classList.toggle('sidebar-hidden');
        }
        updateExpanded();
    });
    backdrop.addEventListener('click', closeMobile);
    mobile.addEventListener('change', () => { closeMobile(); updateExpanded(); });
    updateExpanded();

    const period = document.getElementById('dashboard-period');
    if (period) {
        period.form.querySelector('button').hidden = true;
        period.addEventListener('change', () => period.form.requestSubmit());
    }
    const notifications = page.querySelector('.dashboard-notifications');
    document.addEventListener('click', event => {
        if (!notifications.contains(event.target)) notifications.open = false;
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            if (notifications.open) {
                notifications.open = false;
                notifications.querySelector('summary').focus();
            }
            if (mobile.matches && sidebar.classList.contains('is-open')) {
                closeMobile();
                menu.focus();
            }
        }
    });
})();
// =========================================================
// Pop-up chat window — the Influencer-side twin of the Brand side's chat widget
// (Views/Shared/_AppLayout.cshtml). Same markup, same styles
// (brandora-influencer-shell.css) and the same behaviour: load the thread,
// send text or a photo/video, edit / copy / unsend your own messages, and jump
// to the full inbox. Any element with class "js-open-chat" and
// data-brand-id / data-brand-name opens it (e.g. "Message" on Browse Brands).
// =========================================================
(() => {
    const triggerSelector = '.js-open-chat[data-brand-id]';
    let widget = null;
    let els = {};

    const tokenValue = () => {
        const field = document.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : '';
    };

    const initials = name => (name || '').trim().split(/\s+/).map(w => w[0]).slice(0, 2).join('').toUpperCase();

    function build() {
        if (widget) return;
        const wrap = document.createElement('div');
        wrap.innerHTML = `
<div class="chat-widget-backdrop" id="chatWidget">
    <div class="chat-widget" role="dialog" aria-label="Chat">
        <div class="chat-widget-header">
            <div class="chat-widget-avatar" id="chatWidgetAvatar"></div>
            <div class="chat-widget-name" id="chatWidgetName">Conversation</div>
            <a href="/InfluencerMessages" class="chat-widget-icon-btn" id="chatWidgetExpand" title="Open in Chat" aria-label="Open in Chat">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M9 3H3v6M15 21h6v-6M21 3l-8 8M3 21l8-8" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </a>
            <button type="button" class="chat-widget-icon-btn" id="chatWidgetClose" aria-label="Close chat">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M6 6l12 12M18 6 6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>
            </button>
        </div>
        <div class="chat-widget-body" id="chatWidgetBody">
            <div class="chat-widget-empty">Loading conversation…</div>
        </div>
        <form class="chat-widget-composer" id="chatWidgetForm" enctype="multipart/form-data">
            <input type="hidden" name="brandId" id="chatWidgetBrandId" />
            <input type="file" name="mediaFile" id="chatWidgetFile" accept="image/*,video/*" hidden />
            <button type="button" class="chat-widget-attach-btn" id="chatWidgetAttachBtn" aria-label="Attach a file">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M21 11.5 12.5 20a4.5 4.5 0 0 1-6.4-6.4L14.6 5a3 3 0 0 1 4.3 4.3L10.4 18a1.5 1.5 0 0 1-2.1-2.1l7.1-7.1" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </button>
            <input type="text" name="body" id="chatWidgetInput" placeholder="Type a message…" autocomplete="off" />
            <button type="submit" class="chat-widget-send" aria-label="Send message">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M22 2 11 13M22 2 15 22l-4-9-9-4 20-7Z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </button>
        </form>
    </div>
</div>
<div class="cw-confirm-backdrop" id="chatWidgetConfirm">
    <div class="cw-confirm" role="alertdialog" aria-modal="true" aria-labelledby="chatWidgetConfirmTitle">
        <div class="cw-confirm-icon">!</div>
        <div class="cw-confirm-title" id="chatWidgetConfirmTitle">Unsend this message?</div>
        <p class="cw-confirm-text">This will remove the message for both of you.</p>
        <div class="cw-confirm-actions">
            <button type="button" class="cw-confirm-cancel">Cancel</button>
            <button type="button" class="cw-confirm-ok">Unsend</button>
        </div>
    </div>
</div>`;
        while (wrap.firstElementChild) document.body.appendChild(wrap.firstElementChild);

        widget = document.getElementById('chatWidget');
        els = {
            body: document.getElementById('chatWidgetBody'),
            name: document.getElementById('chatWidgetName'),
            avatar: document.getElementById('chatWidgetAvatar'),
            brandId: document.getElementById('chatWidgetBrandId'),
            expand: document.getElementById('chatWidgetExpand'),
            form: document.getElementById('chatWidgetForm'),
            input: document.getElementById('chatWidgetInput'),
            close: document.getElementById('chatWidgetClose'),
            attach: document.getElementById('chatWidgetAttachBtn'),
            file: document.getElementById('chatWidgetFile'),
            confirm: document.getElementById('chatWidgetConfirm')
        };
        wire();
    }

    function scrollToBottom() {
        const pane = document.getElementById('chatWidgetMessages');
        if (pane) pane.scrollTop = pane.scrollHeight;
    }

    function render(html) {
        els.body.innerHTML = html;
        const pane = document.getElementById('chatWidgetMessages');
        if (pane && pane.dataset.conversationId) {
            els.expand.href = '/InfluencerMessages?open=' + pane.dataset.conversationId;
        }
        scrollToBottom();
    }

    function post(url, formData) {
        formData.set('brandId', els.brandId.value);
        formData.set('__RequestVerificationToken', tokenValue());
        return fetch(url, {
            method: 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: formData
        }).then(r => {
            if (!r.ok) throw new Error('Request failed');
            return r.text();
        }).then(render);
    }

    function askConfirm(onConfirm) {
        const box = els.confirm;
        const close = () => {
            box.classList.remove('is-open');
            document.removeEventListener('keydown', onKey);
        };
        const onKey = e => { if (e.key === 'Escape') close(); };
        box.querySelector('.cw-confirm-cancel').onclick = close;
        box.querySelector('.cw-confirm-ok').onclick = () => { close(); onConfirm(); };
        box.onclick = e => { if (e.target === box) close(); };
        document.addEventListener('keydown', onKey);
        box.classList.add('is-open');
    }

    function closeWidget() {
        widget.classList.remove('is-open');
    }

    window.openChatWidget = function (brandId, name, hue) {
        build();
        els.name.textContent = name || 'Conversation';
        els.avatar.textContent = initials(name);
        els.avatar.style.background = hue !== undefined && hue !== ''
            ? 'hsl(' + hue + 'deg 70% 45%)'
            : 'rgba(255,255,255,.25)';
        els.brandId.value = brandId;
        els.expand.href = '/InfluencerMessages';
        els.body.innerHTML = '<div class="chat-widget-empty">Loading conversation…</div>';
        widget.classList.add('is-open');
        els.input.focus();

        fetch('/InfluencerMessages/Widget?brandId=' + encodeURIComponent(brandId), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => { if (!r.ok) throw new Error('Request failed'); return r.text(); })
            .then(render)
            .catch(() => { els.body.innerHTML = '<div class="chat-widget-empty">Couldn\'t load this conversation.</div>'; });
    };

    function wire() {
        els.close.addEventListener('click', closeWidget);

        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && widget.classList.contains('is-open') && !els.confirm.classList.contains('is-open')) closeWidget();
        });

        els.form.addEventListener('submit', e => {
            e.preventDefault();
            const text = els.input.value.trim();
            if (!text) return;
            els.input.value = '';
            const formData = new FormData();
            formData.set('body', text);
            post('/InfluencerMessages/SendWidget', formData).catch(() => { els.input.value = text; });
        });

        els.attach.addEventListener('click', () => els.file.click());

        els.file.addEventListener('change', () => {
            const file = els.file.files && els.file.files[0];
            if (!file) return;
            const formData = new FormData();
            formData.set('mediaFile', file);
            post('/InfluencerMessages/SendWidget', formData).catch(() => {}).finally(() => { els.file.value = ''; });
        });

        // Delegated per-message actions inside the fetched, repeatedly-replaced list.
        els.body.addEventListener('click', e => {
            const menuToggle = e.target.closest('[data-widget-menu-toggle]');
            if (menuToggle) {
                const menu = document.getElementById('chatWidgetMenu-' + menuToggle.dataset.widgetMenuToggle);
                const wasOpen = menu.classList.contains('is-open');
                els.body.querySelectorAll('.chat-widget-menu.is-open').forEach(m => m.classList.remove('is-open'));
                if (!wasOpen) menu.classList.add('is-open');
                return;
            }

            const copyBtn = e.target.closest('[data-widget-copy]');
            if (copyBtn) {
                const text = copyBtn.dataset.copyText || '';
                if (navigator.clipboard) navigator.clipboard.writeText(text).catch(() => {});
                copyBtn.closest('.chat-widget-menu').classList.remove('is-open');
                return;
            }

            const editBtn = e.target.closest('[data-widget-edit]');
            if (editBtn) {
                const id = editBtn.dataset.widgetEdit;
                document.getElementById('chatWidgetEditForm-' + id).hidden = false;
                document.getElementById('chatWidgetBubble-' + id).style.display = 'none';
                editBtn.closest('.chat-widget-menu').classList.remove('is-open');
                return;
            }

            const cancelBtn = e.target.closest('[data-widget-edit-cancel]');
            if (cancelBtn) {
                const id = cancelBtn.dataset.widgetEditCancel;
                document.getElementById('chatWidgetEditForm-' + id).hidden = true;
                document.getElementById('chatWidgetBubble-' + id).style.display = '';
                return;
            }

            const saveBtn = e.target.closest('[data-widget-edit-save]');
            if (saveBtn) {
                const id = saveBtn.dataset.widgetEditSave;
                const newBody = document.getElementById('chatWidgetEditInput-' + id).value.trim();
                if (!newBody) return;
                const formData = new FormData();
                formData.set('messageId', id);
                formData.set('body', newBody);
                post('/InfluencerMessages/EditMessageWidget', formData).catch(() => {});
                return;
            }

            const unsendBtn = e.target.closest('[data-widget-unsend]');
            if (unsendBtn) {
                const id = unsendBtn.dataset.widgetUnsend;
                unsendBtn.closest('.chat-widget-menu').classList.remove('is-open');
                askConfirm(() => {
                    const formData = new FormData();
                    formData.set('messageId', id);
                    post('/InfluencerMessages/DeleteMessageWidget', formData).catch(() => {});
                });
                return;
            }

            els.body.querySelectorAll('.chat-widget-menu.is-open').forEach(m => m.classList.remove('is-open'));
        });
    }

    // Open from any "Message" trigger. Triggers may sit inside a no-JS fallback
    // form (POST /InfluencerMessages/StartWithBrand); with JS we open the pop-up instead.
    document.addEventListener('click', e => {
        const trigger = e.target.closest(triggerSelector);
        if (!trigger) return;
        e.preventDefault();
        window.openChatWidget(trigger.dataset.brandId, trigger.dataset.brandName, trigger.dataset.hue);
    });
})();
