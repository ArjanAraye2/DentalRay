// ReSiRai - Study Type Lookup
// Converts the existing New/Edit StudyType text inputs to database-backed selects
// without changing the rest of the current Study form layout.
(function () {
    'use strict';

    let studyTypes = [];

    async function loadStudyTypes() {
        const response = await fetch('/api/studytypes');
        // پیش از ورود (صفحهٔ ورود) پاسخِ 401 و بدونِ بدنه است؛ json() با
        // «Unexpected end of JSON input» منفجر می‌شود و انتخابگرها غیرفعال
        // می‌شدند. نبودِ ورود خطا نیست — بی‌صدا رد می‌شویم؛ بعد از ورود
        // (reload) دوباره بارگذاری می‌شود.
        if (response.status === 401 || response.status === 403) return false;
        let result = null;
        try { result = await response.json(); } catch { return false; }
        if (!result) return false;
        if (!response.ok || result.success === false)
            throw new Error(result.message || 'دریافت انواع Study انجام نشد.');
        studyTypes = result.studyTypes || [];
        return true;
    }

    function replaceWithSelect(inputId) {
        const old = document.getElementById(inputId);
        if (!old || old.tagName === 'SELECT') return old;

        const select = document.createElement('select');
        select.id = old.id;
        select.name = old.name || '';
        select.required = true;
        select.className = old.className;
        old.replaceWith(select);
        return select;
    }

    function fill(select) {
        if (!select) return;
        const current = String(select.value || '');
        select.innerHTML = '<option value="">انتخاب نوع Study</option>';
        studyTypes.forEach(type => {
            const option = document.createElement('option');
            option.value = String(type.studyTypeID);
            option.textContent = type.studyTypeName;
            select.appendChild(option);
        });
        if (current) select.value = current;
    }

    function studyTypeId(select) {
        const value = Number(select?.value || 0);
        if (!Number.isInteger(value) || value <= 0)
            throw new Error('نوع Study را انتخاب کنید.');
        return value;
    }

    async function init() {
        // پیش از ورود (فرمِ ورود روی صفحه است) اصلاً درخواست نمی‌زنیم؛ نه خطا
        // در کنسول می‌نشیند و نه انتخابگری غیرفعال می‌شود.
        if (document.querySelector('.login-shell')) return;
        const newSelect = replaceWithSelect('newStudyType');
        const editSelect = replaceWithSelect('editStudyType');
        if (!newSelect && !editSelect) return;

        try {
            const loaded = await loadStudyTypes();
            // پیش از ورود بارگذاری انجام نشده؛ این خطا نیست و بعد از ورود
            // (reload) دوباره اجرا می‌شود. انتخابگرها نباید غیرفعال شوند.
            if (!loaded) return;
            fill(newSelect);
            fill(editSelect);
        } catch (error) {
            console.error(error);
            [newSelect, editSelect].forEach(x => { if (x) x.disabled = true; });
            return;
        }

        // Compatibility is needed only for an older caller that still sends the
        // removed free-text StudyType field. Modern app.js already supplies a valid
        // StudyTypeID and must pass through unchanged (including Study Details PUT).
        const originalFetch = window.fetch.bind(window);
        window.fetch = async function (input, init) {
            try {
                const url = typeof input === 'string' ? input : input?.url || '';
                const method = String(init?.method || 'GET').toUpperCase();
                if (/\/api\/radiologystudies(?:\/\d+)?$/i.test(url) && (method === 'POST' || method === 'PUT') && typeof init?.body === 'string') {
                    const body = JSON.parse(init.body);
                    if (Number.isInteger(Number(body.studyTypeID)) && Number(body.studyTypeID) > 0)
                        return originalFetch(input, init);
                    const select = method === 'POST' ? document.getElementById('newStudyType') : document.getElementById('editStudyType');
                    body.studyTypeID = studyTypeId(select);
                    delete body.studyType;
                    init = { ...init, body: JSON.stringify(body) };
                }
            } catch (error) {
                return Promise.reject(error);
            }
            return originalFetch(input, init);
        };

        // app.js stores the selected Study in module/global lexical state rather than
        // window.selectedStudy. When the edit section opens, read the authoritative
        // Study from the backend and select its StudyTypeID.
        const editSection = document.getElementById('editStudySection');
        if (editSection) {
            let loadSequence = 0;
            new MutationObserver(async () => {
                if (editSection.classList.contains('hidden') || !editSelect) return;
                const subtitle = document.getElementById('editStudySubtitle')?.textContent || '';
                const match = subtitle.match(/(\d+)/);
                const studyID = match ? Number(match[1]) : 0;
                if (!studyID) return;
                const sequence = ++loadSequence;
                try {
                    const response = await originalFetch(`/api/radiologystudies/${studyID}`);
                    const result = await response.json();
                    if (sequence !== loadSequence || !response.ok || result.success === false) return;
                    const id = result.study?.studyTypeID;
                    if (id) editSelect.value = String(id);
                } catch (error) {
                    console.error('Study Type synchronization failed.', error);
                }
            }).observe(editSection, { attributes: true, attributeFilter: ['class'] });
        }
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', init);
    else
        init();
})();
