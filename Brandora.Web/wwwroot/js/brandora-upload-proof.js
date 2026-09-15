(() => {
    'use strict';
    const form = document.getElementById('proofForm');
    if (!form) return;
    const campaigns = JSON.parse(document.getElementById('proofCampaignData').textContent);
    const byId = id => document.getElementById(id);
    const campaignSelect = byId('campaignSelect');
    const milestoneSelect = byId('milestoneSelect');
    const fileInput = byId('proofFile');
    const postLink = byId('proofUrl');
    const notes = byId('proofNotes');
    const continueBtn = byId('continueBtn');
    const dialog = byId('reviewDialog');
    let campaign = null;
    let milestone = null;
    let reviewed = false;
    const money = amount => '৳' + Number(amount).toLocaleString('en-US');

    function setStep(current) {
        document.querySelectorAll('.proof-steps li').forEach(item => {
            const step = Number(item.dataset.step);
            item.classList.toggle('is-active', step === current);
            item.classList.toggle('is-done', step < current);
            if (step === current) item.setAttribute('aria-current', 'step');
            else item.removeAttribute('aria-current');
        });
    }
    function updateState() {
        reviewed = false;
        continueBtn.disabled = !(milestone && (fileInput.files.length || postLink.value.trim()));
        setStep(!campaign ? 1 : !milestone ? 2 : 3);
    }
    function showImage(container, url, name) {
        container.replaceChildren();
        const fallback = (name || 'Campaign').split(/s+/).filter(Boolean).slice(0, 2).map(word => word[0]).join('').toUpperCase();
        container.textContent = fallback;
        if (!url) return;
        const image = document.createElement('img');
        image.alt = '';
        image.src = url;
        image.addEventListener('error', () => { container.textContent = fallback; });
        container.replaceChildren(image);
    }
    campaignSelect.addEventListener('change', () => {
        campaign = campaigns.find(item => String(item.id) === campaignSelect.value) || null;
        milestone = null;
        milestoneSelect.replaceChildren(new Option('Choose a milestone', ''));
        milestoneSelect.disabled = !campaign;
        byId('milestoneLabel').textContent = 'Select a milestone';
        byId('milestoneSubtitle').textContent = campaign ? 'Choose your completed milestone' : 'Choose a campaign first';
        byId('detailsMilestone').textContent = 'Choose a milestone';
        byId('campaignEmpty').hidden = !!campaign;
        byId('campaignDetails').hidden = !campaign;
        byId('campaignLabel').textContent = campaign?.title || 'Select a campaign';
        byId('campaignSubtitle').textContent = campaign ? [campaign.brand, campaign.niche].filter(Boolean).join(' • ') : 'Your active collaborations';
        showImage(byId('campaignIcon'), campaign?.mediaUrl, campaign?.brand);
        if (campaign) {
            campaign.milestones.forEach(item => milestoneSelect.add(new Option(item.title + ' (' + money(item.amount) + ')', item.id)));
            showImage(byId('detailsImage'), campaign.mediaUrl, campaign.brand);
            byId('detailsTitle').textContent = campaign.title;
            byId('detailsBrand').textContent = [campaign.brand, campaign.niche].filter(Boolean).join(' • ');
            byId('detailsDeadline').textContent = campaign.deadline || 'No deadline';
            byId('detailsPlatform').textContent = campaign.platform || 'Not specified';
        }
        updateState();
    });
    milestoneSelect.addEventListener('change', () => {
        milestone = campaign?.milestones.find(item => String(item.id) === milestoneSelect.value) || null;
        byId('milestoneLabel').textContent = milestone?.title || 'Select a milestone';
        byId('milestoneSubtitle').textContent = milestone ? 'Milestone payment: ' + money(milestone.amount) : 'Choose your completed milestone';
        byId('detailsMilestone').textContent = milestone?.title || 'Choose a milestone';
        updateState();
    });

    const limits = {
        'image/jpeg': 15, 'image/png': 15, 'image/webp': 15, 'image/gif': 15,
        'video/mp4': 80, 'video/webm': 80, 'video/quicktime': 80, 'application/pdf': 100
    };
    function fileError(message) {
        byId('fileError').textContent = message;
        byId('fileError').hidden = !message;
    }
    function renderFile() {
        const list = byId('fileList');
        list.replaceChildren();
        fileError('');
        const file = fileInput.files[0];
        if (file) {
            const limit = limits[file.type];
            if (!limit || !file.size || file.size > limit * 1024 * 1024) {
                fileError(!limit ? 'Choose a JPG, PNG, WEBP, GIF, MP4, WEBM, MOV, or PDF file.' : !file.size ? 'This file is empty. Choose another file.' : 'This file must be ' + limit + 'MB or smaller.');
                fileInput.value = '';
                updateState();
                return;
            }
            const chip = document.createElement('div');
            chip.className = 'proof-file-chip';
            const icon = document.createElement('i');
            icon.className = 'bi bi-file-earmark-check';
            icon.setAttribute('aria-hidden', 'true');
            const label = document.createElement('span');
            label.textContent = file.name + ' · ' + (file.size / 1024 / 1024).toFixed(1) + ' MB';
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.textContent = '×';
            remove.setAttribute('aria-label', 'Remove ' + file.name);
            remove.addEventListener('click', () => {
                fileInput.value = '';
                renderFile();
                byId('chooseFileBtn').focus();
            });
            chip.append(icon, label, remove);
            list.append(chip);
        }
        updateState();
    }
    byId('chooseFileBtn').addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', renderFile);
    const dropzone = byId('dropzone');
    ['dragenter', 'dragover'].forEach(type => dropzone.addEventListener(type, event => {
        event.preventDefault();
        dropzone.classList.add('is-dragover');
    }));
    ['dragleave', 'drop'].forEach(type => dropzone.addEventListener(type, event => {
        event.preventDefault();
        dropzone.classList.remove('is-dragover');
    }));
    dropzone.addEventListener('drop', event => {
        const files = event.dataTransfer?.files;
        if (!files?.length) return;
        if (files.length > 1) {
            fileError('Choose one proof file per submission.');
            return;
        }
        fileInput.files = files;
        renderFile();
    });
    postLink.addEventListener('input', () => {
        postLink.setCustomValidity('');
        updateState();
    });
    notes.addEventListener('input', () => {
        byId('notesCount').textContent = notes.value.length + '/500';
        reviewed = false;
    });
    function openReview() {
        postLink.setCustomValidity('');
        if (postLink.value.trim()) {
            try {
                const url = new URL(postLink.value.trim());
                if (!['http:', 'https:'].includes(url.protocol)) throw new Error();
            } catch {
                postLink.setCustomValidity('Enter a valid http or https post link.');
            }
        }
        if (!form.reportValidity() || continueBtn.disabled) return;
        const details = byId('reviewDetails');
        details.replaceChildren();
        const rows = [
            ['Campaign', campaign.title], ['Milestone', milestone.title],
            ['Proof file', fileInput.files[0]?.name], ['Post link', postLink.value.trim()],
            ['Additional notes', notes.value.trim()]
        ];
        rows.filter(([, value]) => value).forEach(([label, value]) => {
            const row = document.createElement('div');
            const term = document.createElement('dt');
            const description = document.createElement('dd');
            term.textContent = label;
            description.textContent = value;
            row.append(term, description);
            details.append(row);
        });
        byId('reviewHint').hidden = !(fileInput.files.length && postLink.value.trim());
        reviewed = true;
        setStep(4);
        dialog.showModal();
        byId('reviewTitle').focus();
    }
    continueBtn.addEventListener('click', openReview);
    byId('editProofBtn').addEventListener('click', () => dialog.close());
    dialog.addEventListener('close', () => {
        updateState();
        continueBtn.focus();
    });
    form.addEventListener('submit', event => {
        if (!reviewed || !dialog.open || continueBtn.disabled) {
            event.preventDefault();
            openReview();
            return;
        }
        byId('submitProofBtn').disabled = true;
        byId('submitProofBtn').textContent = 'Submitting…';
    });
    updateState();
})();