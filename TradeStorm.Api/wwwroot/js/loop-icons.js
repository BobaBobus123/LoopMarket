// LoopMarket Shared Vector SVG Icon System
(function (global) {
    'use strict';

    const ICONS = {
        // --- Core UI & Navigation ---
        home: '<path d="M3 10.5L12 3l9 7.5V20a2 2 0 0 1-2 2h-4.5v-6.5h-5V22H5a2 2 0 0 1-2-2z" fill="currentColor" fill-opacity="0.12"/><path d="M3 10.5L12 3l9 7.5V20a2 2 0 0 1-2 2h-4.5v-6.5h-5V22H5a2 2 0 0 1-2-2z"/>',
        wrench: '<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" fill="currentColor" fill-opacity="0.12"/><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>',
        hammer: '<path d="m15 12-8.5 8.5c-.83.83-2.17.83-3 0 0 0 0 0 0 0a2.12 2.12 0 0 1 0-3L12 9"/><path d="M17.64 15 22 10.64"/><path d="m20.91 11.7-1.25-1.25c-.6-.6-.93-1.4-.93-2.25v-.86L16.01 4.6a5.56 5.56 0 0 0-3.94-1.64H9l.92.82A6.18 6.18 0 0 1 12 8.4v1.56l2 2h2.47l2.26 1.91"/>',
        chat: '<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" fill="currentColor" fill-opacity="0.12"/><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>',
        user: '<path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4" fill="currentColor" fill-opacity="0.14"/><circle cx="12" cy="7" r="4"/>',
        users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4" fill="currentColor" fill-opacity="0.14"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>',
        bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" fill="currentColor" fill-opacity="0.12"/><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/>',
        bellOff: '<path d="M8.7 3A6 6 0 0 1 18 8a21.3 21.3 0 0 0 .6 5"/><path d="M17 17H3s3-2 3-9a4.67 4.67 0 0 1 .3-1.7"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/><path d="m2 2 20 20"/>',
        sun: '<circle cx="12" cy="12" r="4" fill="#f59e0b" fill-opacity="0.25" stroke="#f59e0b"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" stroke="#f59e0b"/>',
        moon: '<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z" fill="#38bdf8" fill-opacity="0.2" stroke="#38bdf8"/>',
        search: '<circle cx="11" cy="11" r="8" fill="currentColor" fill-opacity="0.08"/><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
        sliders: '<line x1="4" x2="4" y1="21" y2="14"/><line x1="4" x2="4" y1="10" y2="3"/><line x1="12" x2="12" y1="21" y2="12"/><line x1="12" x2="12" y1="8" y2="3"/><line x1="20" x2="20" y1="21" y2="16"/><line x1="20" x2="20" y1="12" y2="3"/><line x1="2" x2="6" y1="14" y2="14"/><line x1="10" x2="14" y1="8" y2="8"/><line x1="18" x2="22" y1="16" y2="16"/>',
        settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>',
        plus: '<path d="M12 5v14M5 12h14"/>',
        x: '<path d="M18 6 6 18M6 6l12 12"/>',
        check: '<polyline points="20 6 9 17 4 12"/>',
        checkCircle: '<circle cx="12" cy="12" r="10" fill="#10b981" fill-opacity="0.16" stroke="#10b981"/><path d="m9 12 2 2 4-4" stroke="#10b981"/>',
        alertTriangle: '<path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" fill="#f59e0b" fill-opacity="0.16" stroke="#f59e0b"/><path d="M12 9v4M12 17h.01" stroke="#f59e0b"/>',
        alertCircle: '<circle cx="12" cy="12" r="10" fill="#ef4444" fill-opacity="0.16" stroke="#ef4444"/><line x1="12" x2="12" y1="8" y2="12" stroke="#ef4444"/><line x1="12" x2="12.01" y1="16" y2="16" stroke="#ef4444"/>',
        ban: '<circle cx="12" cy="12" r="10" fill="#ef4444" fill-opacity="0.14" stroke="#ef4444"/><path d="m4.9 4.9 14.2 14.2" stroke="#ef4444"/>',
        info: '<circle cx="12" cy="12" r="10" fill="#3b82f6" fill-opacity="0.14" stroke="#3b82f6"/><path d="M12 16v-4M12 8h.01" stroke="#3b82f6"/>',
        trash: '<path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/>',
        edit: '<path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/>',
        pencil: '<path d="M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z"/>',
        eye: '<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',
        eyeOff: '<path d="M9.88 9.88a3 3 0 1 0 4.24 4.24"/><path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68"/><path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61"/><line x1="2" x2="22" y1="2" y2="22"/>',
        copy: '<rect width="14" height="14" x="8" y="8" rx="2" ry="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/>',
        link: '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>',
        externalLink: '<path d="M15 3h6v6"/><path d="M10 14 21 3"/><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>',
        share: '<circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><line x1="8.59" x2="15.42" y1="13.51" y2="17.49"/><line x1="15.41" x2="8.59" y1="6.51" y2="10.49"/>',
        refresh: '<path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/>',
        upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" x2="12" y1="3" y2="15"/>',
        download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/>',
        save: '<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/>',
        logOut: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/>',
        logIn: '<path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"/><polyline points="10 17 15 12 10 7"/><line x1="15" x2="3" y1="12" y2="12"/>',
        arrowRight: '<path d="M5 12h14"/><path d="m12 5 7 7-7 7"/>',

        // --- Marketplace, Badges & Trust ---
        crown: '<path d="M2 4l3 12h14l3-12-6 7-4-7-4 7-6-7zm3 16h14" fill="#f59e0b" fill-opacity="0.22" stroke="#f59e0b"/>',
        gem: '<path d="M6 3h12l4 6-10 13L2 9Z" fill="#38bdf8" fill-opacity="0.22" stroke="#38bdf8"/><path d="M11 3 8 9l4 13 4-13-3-6" stroke="#38bdf8"/><path d="M2 9h20" stroke="#38bdf8"/>',
        star: '<polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" stroke="#f59e0b"/>',
        starFilled: '<polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" fill="#f59e0b" stroke="#f59e0b"/>',
        shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" fill="#10b981" fill-opacity="0.16" stroke="#10b981"/>',
        shieldCheck: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" fill="#10b981" fill-opacity="0.18" stroke="#10b981"/><path d="m9 12 2 2 4-4" stroke="#10b981"/>',
        shieldAlert: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" fill="#f59e0b" fill-opacity="0.16" stroke="#f59e0b"/><path d="M12 8v4M12 16h.01" stroke="#f59e0b"/>',
        lock: '<rect width="18" height="11" x="3" y="11" rx="2" ry="2" fill="currentColor" fill-opacity="0.12"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
        unlock: '<rect width="18" height="11" x="3" y="11" rx="2" ry="2" fill="currentColor" fill-opacity="0.12"/><path d="M7 11V7a5 5 0 0 1 9.9-1"/>',
        key: '<circle cx="7.5" cy="15.5" r="5.5" fill="currentColor" fill-opacity="0.12"/><path d="m21 2-9.6 9.6"/><path d="m15.5 7.5 3 3L22 7l-3-3"/>',
        award: '<circle cx="12" cy="8" r="6" fill="#f59e0b" fill-opacity="0.2" stroke="#f59e0b"/><path d="M15.477 12.89 17 22l-5-3-5 3 1.523-9.11" stroke="#f59e0b"/>',
        trophy: '<path d="M6 9H4.5a2.5 2.5 0 0 1 0-5H6"/><path d="M18 9h1.5a2.5 2.5 0 0 0 0-5H18"/><path d="M4 22h16"/><path d="M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22"/><path d="M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22"/><path d="M18 2H6v7a6 6 0 0 0 12 0V2Z" fill="#f59e0b" fill-opacity="0.2" stroke="#f59e0b"/>',
        sparkles: '<path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275L12 3Z" fill="#f59e0b" fill-opacity="0.22" stroke="#f59e0b"/><path d="M5 3v4M3 5h4" stroke="#f59e0b"/>',
        rocket: '<path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z" fill="#f97316" fill-opacity="0.25"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" fill="currentColor" fill-opacity="0.14"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/>',
        flame: '<path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z" fill="#f97316" fill-opacity="0.22" stroke="#f97316"/>',
        megaphone: '<path d="m3 11 18-5v12L3 13v-2z" fill="currentColor" fill-opacity="0.15"/><path d="m3 11 18-5v12L3 13v-2z"/><path d="M11.6 16.8a3 3 0 1 1-5.8-1.6"/>',
        gift: '<rect x="3" y="8" width="18" height="4" rx="1" fill="#10b981" fill-opacity="0.18"/><path d="M12 8v13"/><path d="M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7"/><path d="M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5"/>',
        heart: '<path d="M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z" fill="#ef4444" fill-opacity="0.2" stroke="#ef4444"/>',
        thumbsUp: '<path d="M7 10v12"/><path d="M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z" fill="currentColor" fill-opacity="0.12"/>',
        tag: '<path d="M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z" fill="currentColor" fill-opacity="0.12"/><circle cx="7.5" cy="7.5" r=".5" fill="currentColor"/>',
        mapPin: '<path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z" fill="#10b981" fill-opacity="0.16"/><circle cx="12" cy="10" r="3"/>',
        globe: '<circle cx="12" cy="12" r="10" fill="currentColor" fill-opacity="0.08"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/>',
        package: '<path d="m7.5 4.27 9 5.15"/><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" fill="currentColor" fill-opacity="0.1"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/>',
        truck: '<path d="M14 18V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v11a1 1 0 0 0 1 1h2"/><path d="M15 18H9"/><path d="M19 18h2a1 1 0 0 0 1-1v-3.65a1 1 0 0 0-.22-.624l-3.48-4.35A1 1 0 0 0 17.52 8H14"/><circle cx="17" cy="18" r="2"/><circle cx="7" cy="18" r="2"/>',
        creditCard: '<rect width="20" height="14" x="2" y="5" rx="2" fill="currentColor" fill-opacity="0.1"/><line x1="2" x2="22" y1="10" y2="10"/>',
        wallet: '<path d="M19 7V4a1 1 0 0 0-1-1H5a2 2 0 0 0 0 4h15a1 1 0 0 1 1 1v4h-3a2 2 0 0 0 0 4h3a1 1 0 0 0 1-1v-2a1 1 0 0 0-1-1"/><path d="M3 5v14a2 2 0 0 0 2 2h15a1 1 0 0 0 1-1v-4"/>',
        banknote: '<rect width="20" height="12" x="2" y="6" rx="2" fill="#10b981" fill-opacity="0.14"/><circle cx="12" cy="12" r="2"/><path d="M6 12h.01M18 12h.01"/>',
        receipt: '<path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z"/><path d="M16 8h-6a2 2 0 1 0 0 4h4a2 2 0 1 1 0 4H8"/><path d="M12 17.5v-11"/>',
        shoppingBag: '<path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z" fill="currentColor" fill-opacity="0.1"/><path d="M3 6h18"/><path d="M16 10a4 4 0 0 1-8 0"/>',
        store: '<path d="m2 7 4.41-4.41A2 2 0 0 1 7.83 2h8.34a2 2 0 0 1 1.42.59L22 7"/><path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8"/><path d="M15 22v-4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4"/><path d="M2 7h20"/><path d="M22 7v3a2 2 0 0 1-2 2a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 16 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 12 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 8 12a2.7 2.7 0 0 1-1.59-.63.7.7 0 0 0-.82 0A2.7 2.7 0 0 1 4 12a2 2 0 0 1-2-2V7"/>',
        briefcase: '<rect width="20" height="14" x="2" y="7" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"/>',
        building: '<rect width="16" height="20" x="4" y="2" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/><path d="M9 22v-4h6v4"/><path d="M8 6h.01M16 6h.01M12 6h.01M12 10h.01M12 14h.01M16 10h.01M16 14h.01M8 10h.01M8 14h.01"/>',
        clock: '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>',
        calendar: '<rect width="18" height="18" x="3" y="4" rx="2" ry="2"/><line x1="16" x2="16" y1="2" y2="6"/><line x1="8" x2="8" y1="2" y2="6"/><line x1="3" x2="21" y1="10" y2="10"/>',

        // --- Media, Tech & Documents ---
        camera: '<path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z" fill="currentColor" fill-opacity="0.12"/><circle cx="12" cy="13" r="3"/>',
        image: '<rect width="18" height="18" x="3" y="3" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/>',
        video: '<path d="m22 8-6 4 6 4V8Z" fill="currentColor" fill-opacity="0.15"/><rect width="14" height="12" x="2" y="6" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/>',
        play: '<polygon points="6 3 20 12 6 21 6 3" fill="currentColor"/>',
        phone: '<path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" fill="#10b981" fill-opacity="0.15"/>',
        mail: '<rect width="20" height="16" x="2" y="4" rx="2" fill="currentColor" fill-opacity="0.1"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>',
        fileText: '<path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" fill="currentColor" fill-opacity="0.1"/><polyline points="14 2 14 8 20 8"/><line x1="16" x2="8" y1="13" y2="13"/><line x1="16" x2="8" y1="17" y2="17"/>',
        clipboard: '<rect width="8" height="4" x="8" y="2" rx="1" ry="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/>',
        barChart: '<line x1="12" x2="12" y1="20" y2="10"/><line x1="18" x2="18" y1="20" y2="4"/><line x1="6" x2="6" y1="20" y2="16"/>',
        trendingUp: '<polyline points="22 7 13.5 15.5 8.5 10.5 2 17"/><polyline points="16 7 22 7 22 13"/>',
        folder: '<path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z" fill="currentColor" fill-opacity="0.12"/>',
        code: '<polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/>',
        terminal: '<polyline points="4 17 10 11 4 5"/><line x1="12" x2="20" y1="19" y2="19"/>',
        bot: '<rect width="18" height="12" x="3" y="8" rx="2" fill="currentColor" fill-opacity="0.12"/><path d="M12 8V4H8"/><path d="M2 14h2M20 14h2M15 13v2M9 13v2"/>',
        cookie: '<path d="M12 2a10 10 0 1 0 10 10 4 4 0 0 1-5-5 4 4 0 0 1-5-5" fill="#f59e0b" fill-opacity="0.18" stroke="#f59e0b"/><path d="M8.5 8.5v.01M16 15.5v.01M12 12v.01M11 17v.01M7 14v.01" stroke="#f59e0b"/>',
        flag: '<path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" fill="currentColor" fill-opacity="0.14"/><line x1="4" x2="4" y1="22" y2="15"/>',

        // --- Category Specific Icons ---
        smartphone: '<rect width="14" height="20" x="5" y="2" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/><path d="M12 18h.01"/>',
        laptop: '<path d="M20 16V7a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v9m16 0H4m16 0 1.28 2.55a1 1 0 0 1-.9 1.45H3.62a1 1 0 0 1-.9-1.45L4 16" fill="currentColor" fill-opacity="0.1"/>',
        headphones: '<path d="M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3"/>',
        tv: '<rect width="20" height="15" x="2" y="7" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/><polyline points="17 2 12 7 7 2"/>',
        watch: '<circle cx="12" cy="12" r="6" fill="currentColor" fill-opacity="0.1"/><polyline points="12 10 12 12 13 13"/><path d="m16.13 7.66-.81-4.05a2 2 0 0 0-2-1.61h-2.68a2 2 0 0 0-2 1.61l-.78 4.05"/><path d="m7.88 16.36.8 4a2 2 0 0 0 2 1.61h2.72a2 2 0 0 0 2-1.61l.81-4.05"/>',
        gamepad: '<line x1="6" x2="10" y1="12" y2="12"/><line x1="8" x2="8" y1="10" y2="14"/><line x1="15" x2="15.01" y1="13" y2="13"/><line x1="18" x2="18.01" y1="11" y2="11"/><rect width="20" height="12" x="2" y="6" rx="2" fill="currentColor" fill-opacity="0.1"/>',
        tablet: '<rect width="16" height="20" x="4" y="2" rx="2" ry="2" fill="currentColor" fill-opacity="0.1"/><line x1="12" x2="12.01" y1="18" y2="18"/>',
        zap: '<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" fill="#f59e0b" fill-opacity="0.24" stroke="#f59e0b"/>',
        battery: '<rect width="16" height="10" x="2" y="7" rx="2" ry="2" fill="#10b981" fill-opacity="0.15"/><line x1="22" x2="22" y1="11" y2="13"/>',
        batteryCharging: '<path d="M15 7h1a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2h-2"/><path d="M6 7H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h1"/><path d="m11 7-3 5h4l-3 5" stroke="#10b981"/><line x1="22" x2="22" y1="11" y2="13"/>',
        plug: '<path d="M12 22v-5"/><path d="M9 8V2"/><path d="M15 8V2"/><path d="M18 8v5a4 4 0 0 1-4 4h-4a4 4 0 0 1-4-4V8Z" fill="currentColor" fill-opacity="0.12"/>',
        flashlight: '<path d="M18 6c0 2-2 2-2 4v10a2 2 0 0 1-2 2h-4a2 2 0 0 1-2-2V10c0-2-2-2-2-4V2h12z" fill="currentColor" fill-opacity="0.12"/><line x1="6" x2="18" y1="6" y2="6"/><line x1="12" x2="12" y1="12" y2="12.01"/>',
        shirt: '<path d="M20.38 3.46 16 2a4 4 0 0 1-8 0L3.62 3.46a2 2 0 0 0-1.34 2.23l.58 3.47a1 1 0 0 0 .99.84H6v10c0 1.1.9 2 2 2h8a2 2 0 0 0 2-2V10h2.15a1 1 0 0 0 .99-.84l.58-3.47a2 2 0 0 0-1.34-2.23z" fill="currentColor" fill-opacity="0.12"/>',
        glasses: '<circle cx="6" cy="15" r="4"/><circle cx="18" cy="15" r="4"/><path d="M14 15a2 2 0 0 0-2-2 2 2 0 0 0-2 2"/><path d="M2.5 13 5 7c.7-1.3 1.4-2 3-2"/><path d="M21.5 13 19 7c-.7-1.3-1.5-2-3-2"/>',
        sofa: '<path d="M20 9V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v3"/><path d="M2 11v5a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-5a2 2 0 0 0-4 0v2H6v-2a2 2 0 0 0-4 0Z" fill="currentColor" fill-opacity="0.12"/><path d="M4 18v2M20 18v2"/>',
        bed: '<path d="M2 4v16"/><path d="M2 8h18a2 2 0 0 1 2 2v10"/><path d="M2 17h20"/><path d="M6 8v9"/>',
        utensils: '<path d="M3 2v7c0 1.1.9 2 2 2h4a2 2 0 0 0 2-2V2"/><path d="M7 2v20"/><path d="M21 15V2v0a5 5 0 0 0-5 5v6c0 1.1.9 2 2 2h3Zm0 0v7"/>',
        lightbulb: '<path d="M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5" fill="#f59e0b" fill-opacity="0.2" stroke="#f59e0b"/><path d="M9 18h6M10 22h4"/>',
        sprout: '<path d="M7 20h10"/><path d="M10 20c5.5-2.5.8-6.4 3-10"/><path d="M9.5 9.4c1.1.8 1.8 2.2 2.3 3.7-2 .4-3.5.4-4.8-.3-1.2-.6-2.3-1.9-3-4.2 2.8-.5 4.4 0 5.5.8z" fill="#10b981" fill-opacity="0.2" stroke="#10b981"/><path d="M14.1 6a7 7 0 0 0-1.1 4c1.9-.1 3.3-.6 4.3-1.4 1-1 1.6-2.3 1.7-4.6-2.7.1-4 1-4.9 2z" fill="#10b981" fill-opacity="0.2" stroke="#10b981"/>',
        droplet: '<path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" fill="#38bdf8" fill-opacity="0.2" stroke="#38bdf8"/>',
        layers: '<polygon points="12 2 2 7 12 12 22 7 12 2" fill="currentColor" fill-opacity="0.14"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/>',
        palette: '<circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/><circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/><circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/><circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/><path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z" fill="currentColor" fill-opacity="0.1"/>',
        scissors: '<circle cx="6" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><line x1="20" x2="8.12" y1="4" y2="15.88"/><line x1="14.47" x2="20" y1="14.48" y2="20"/><line x1="8.12" x2="12" y1="8.12" y2="12"/>',
        pill: '<path d="m10.5 20.5 10-10a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z" fill="currentColor" fill-opacity="0.12"/><path d="m8.5 8.5 7 7"/>',
        activity: '<path d="M22 12h-4l-3 9L9 3l-3 9H2"/>',
        car: '<path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2" fill="currentColor" fill-opacity="0.12"/><circle cx="7" cy="17" r="2"/><path d="M9 17h6"/><circle cx="17" cy="17" r="2"/>',
        bike: '<circle cx="18.5" cy="17.5" r="3.5"/><circle cx="5.5" cy="17.5" r="3.5"/><circle cx="15" cy="5" r="1"/><path d="M12 17.5V14l-3-3 4-3 2 3h2"/>',
        bookOpen: '<path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" fill="currentColor" fill-opacity="0.1"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" fill="currentColor" fill-opacity="0.1"/>',
        puzzle: '<path d="M19.439 7.85c-.049.322.059.648.289.878l1.568 1.568c.47.47.706 1.087.706 1.704s-.235 1.233-.706 1.704l-1.611 1.611a.98.98 0 0 1-.837.276c-.47-.07-.802-.48-.968-.925a2.501 2.501 0 1 0-3.214 3.214c.446.166.855.497.925.968a.979.979 0 0 1-.276.837l-1.61 1.61a2.404 2.404 0 0 1-1.705.707 2.402 2.402 0 0 1-1.704-.706l-1.568-1.568a1.026 1.026 0 0 0-.877-.29c-.493.074-.84.504-1.02.968a2.5 2.5 0 1 1-3.237-3.237c.464-.18.894-.527.967-1.02a1.026 1.026 0 0 0-.289-.877l-1.568-1.568A2.402 2.402 0 0 1 1.998 12c0-.617.236-1.234.706-1.704L4.23 8.77c.24-.24.581-.353.917-.303.515.077.877.528 1.073 1.01a2.5 2.5 0 1 0 3.259-3.259c-.482-.196-.933-.558-1.01-1.073-.05-.336.062-.676.303-.917l1.525-1.525A2.402 2.402 0 0 1 12 1.998c.617 0 1.234.236 1.704.706l1.568 1.568c.23.23.556.338.877.29.493-.074.84-.504 1.02-.968a2.5 2.5 0 1 1 3.237 3.237c-.464.18-.894.527-.967 1.02Z"/>',
        tent: '<path d="M3.5 21 14 3"/><path d="M20.5 21 10 3"/><path d="M15.5 21 12 15l-3.5 6"/><path d="M2 21h20"/>',
        dumbbell: '<path d="m6.5 6.5 11 11"/><path d="m21 21-1-1"/><path d="m3 3 1 1"/><path d="m18 22 4-4"/><path d="m2 6 4-4"/><path d="m3 10 7-7"/><path d="m14 21 7-7"/>',
        compass: '<circle cx="12" cy="12" r="10"/><polygon points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88 16.24 7.76" fill="currentColor" fill-opacity="0.16"/>',
        paw: '<circle cx="11" cy="4" r="2"/><circle cx="18" cy="8" r="2"/><circle cx="20" cy="16" r="2"/><path d="M9 10a5 5 0 0 1 5 5v3.5a3.5 3.5 0 0 1-6.84 1.045Q6.52 17.48 4.46 16.84A3.5 3.5 0 0 1 5.5 10Z" fill="currentColor" fill-opacity="0.14"/>',
        graduationCap: '<path d="M21.42 10.922a1 1 0 0 0-.019-1.838L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.832l8.57 3.908a2 2 0 0 0 1.66 0z" fill="currentColor" fill-opacity="0.14"/><path d="M22 10v6"/><path d="M6 12.5V16a6 3 0 0 0 12 0v-3.5"/>',
        smile: '<circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" x2="9.01" y1="9" y2="9"/><line x1="15" x2="15.01" y1="9" y2="9"/>',
        disc: '<circle cx="12" cy="12" r="10" fill="currentColor" fill-opacity="0.1"/><circle cx="12" cy="12" r="2"/>',
        radio: '<path d="M4.9 19.1C1 15.2 1 8.8 4.9 4.9"/><path d="M7.8 16.2c-2.3-2.3-2.3-6.1 0-8.5"/><circle cx="12" cy="12" r="2"/><path d="M16.2 7.8c2.3 2.3 2.3 6.1 0 8.5"/><path d="M19.1 4.9C23 8.8 23 15.1 19.1 19"/>',
        headphonesMic: '<path d="M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3Z" fill="currentColor" fill-opacity="0.14"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/><line x1="12" x2="12" y1="19" y2="22"/>',
        box: '<path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" fill="currentColor" fill-opacity="0.1"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/>'
    };

    const CATEGORY_EMOJI_MAP = {
        '\u{1F4F1}': 'smartphone',
        '\u{1F4BB}': 'laptop',
        '\u{1F3A7}': 'headphones',
        '\u{1F4FA}': 'tv',
        '\u231A': 'watch',
        '\u{1F3AE}': 'gamepad',
        '\u{1F4DF}': 'tablet',
        '\u26A1': 'zap',
        '\u{1F50B}': 'batteryCharging',
        '\u{1F50C}': 'plug',
        '\u2600': 'sun',
        '\u{1F504}': 'refresh',
        '\u{1FAAB}': 'battery',
        '\u{1F6E1}': 'shield',
        '\u{1F9BA}': 'shieldCheck',
        '\u{1F97E}': 'shield',
        '\u{1F392}': 'shoppingBag',
        '\u{1F526}': 'flashlight',
        '\u{1F52D}': 'compass',
        '\u{1FA79}': 'activity',
        '\u{1F457}': 'shirt',
        '\u{1F454}': 'shirt',
        '\u{1F45F}': 'tag',
        '\u{1F45C}': 'shoppingBag',
        '\u{1F453}': 'glasses',
        '\u{1F476}': 'smile',
        '\u{1F3E0}': 'home',
        '\u{1F6CB}': 'sofa',
        '\u{1F6CF}': 'bed',
        '\u{1F37D}': 'utensils',
        '\u{1F4A1}': 'lightbulb',
        '\u{1F331}': 'sprout',
        '\u{1F9F9}': 'sparkles',
        '\u{1F6E0}': 'wrench',
        '\u{1FA9A}': 'wrench',
        '\u{1F528}': 'hammer',
        '\u{1F527}': 'wrench',
        '\u{1F6BF}': 'droplet',
        '\u{1F9F1}': 'layers',
        '\u{1F3A8}': 'palette',
        '\u{1F6AA}': 'home',
        '\u{1F484}': 'sparkles',
        '\u{1F338}': 'sparkles',
        '\u{1F9F4}': 'droplet',
        '\u{1F487}': 'scissors',
        '\u{1F48A}': 'pill',
        '\u{1FA7A}': 'activity',
        '\u{1F697}': 'car',
        '\u2699': 'settings',
        '\u{1F6DE}': 'disc',
        '\u{1F4FB}': 'radio',
        '\u{1F6B2}': 'bike',
        '\u{1F4DA}': 'bookOpen',
        '\u{1F4D6}': 'bookOpen',
        '\u{1F4BC}': 'briefcase',
        '\u{1F3B2}': 'puzzle',
        '\u270F': 'pencil',
        '\u{1F9F8}': 'puzzle',
        '\u{1F37C}': 'heart',
        '\u{1F6BC}': 'smile',
        '\u26BD': 'trophy',
        '\u{1F3D5}': 'tent',
        '\u{1F3CB}': 'dumbbell',
        '\u{1F3A3}': 'compass',
        '\u{1F93F}': 'droplet',
        '\u{1F43E}': 'paw',
        '\u{1F969}': 'paw',
        '\u{1F9AE}': 'paw',
        '\u{1F69A}': 'truck',
        '\u{1F393}': 'graduationCap',
        '\u{1F3E2}': 'building',
        '\u{1F511}': 'key',
        '\u{1F3F7}': 'tag',
        '\u{1F3E1}': 'home',
        '\u{1F3EC}': 'store',
        '\u{1F33E}': 'sprout',
        '\u{1F381}': 'gift',
        '\u{1F4E6}': 'package',
        '\u{1F48E}': 'gem',
        '\u{1F451}': 'crown',
        '\u2B50': 'starFilled',
        '\u{1F525}': 'flame',
        '\u{1F680}': 'rocket',
        '\u{1F4E2}': 'megaphone',
        '\u{1F514}': 'bell',
        '\u{1F4AC}': 'chat',
        '\u{1F4CD}': 'mapPin',
        '\u{1F4F7}': 'camera',
        '\u{1F3AC}': 'video',
        '\u{1F4B3}': 'creditCard',
        '\u{1F4B0}': 'banknote',
        '\u{1F464}': 'user',
        '\u{1F465}': 'users',
        '\u{1F512}': 'lock',
        '\u2705': 'checkCircle',
        '\u26A0': 'alertTriangle',
        '\u{1F6AB}': 'ban',
        '\u2139': 'info'
    };

    function svg(name, opts) {
        const o = typeof opts === 'number' ? { size: opts } : (opts || {});
        const size = o.size || 18;
        const cls = o.className ? 'loop-svg-icon ' + o.className : 'loop-svg-icon';
        const colorStyle = o.color ? 'color:' + o.color + ';' : '';
        const extraStyle = o.style || '';
        const styleAttr = (colorStyle || extraStyle) ? ' style="' + colorStyle + extraStyle + '"' : '';
        const strokeWidth = o.strokeWidth || 2;
        const body = ICONS[name] || ICONS.tag;
        return '<svg class="' + cls + '" width="' + size + '" height="' + size + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="' + strokeWidth + '" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"' + styleAttr + '>' + body + '</svg>';
    }

    function iconNameFromCategory(iconOrEmoji, categoryName) {
        const cleanEmoji = String(iconOrEmoji || '').replace(/\uFE0F/g, '').trim();
        for (const key of Object.keys(CATEGORY_EMOJI_MAP)) {
            if (cleanEmoji.startsWith(key)) {
                return CATEGORY_EMOJI_MAP[key];
            }
        }
        const n = String(categoryName || iconOrEmoji || '').toLowerCase();
        if (!n) return 'package';
        if (n.includes('смартфон') || n.includes('телефон')) return 'smartphone';
        if (n.includes('ноут') || n.includes('пк') || n.includes('it') || n.includes('дизайн')) return 'laptop';
        if (n.includes('навушн') || n.includes('аудіо')) return 'headphones';
        if (n.includes('телевіз') || n.includes('відео')) return 'tv';
        if (n.includes('годинник')) return 'watch';
        if (n.includes('консол') || n.includes('гейм')) return 'gamepad';
        if (n.includes('планшет')) return 'tablet';
        if (n.includes('електрон')) return 'smartphone';
        if (n.includes('станці') || n.includes('акумулятор')) return 'batteryCharging';
        if (n.includes('генерат')) return 'plug';
        if (n.includes('живлен') || n.includes('енерг') || n.includes('павербанк')) return 'zap';
        if (n.includes('соняч')) return 'sun';
        if (n.includes('військ') || n.includes('тактич') || n.includes('амуніц')) return 'shield';
        if (n.includes('ліхтар')) return 'flashlight';
        if (n.includes('одяг') || n.includes('взутт') || n.includes('аксесуар')) return 'shirt';
        if (n.includes('мебл')) return 'sofa';
        if (n.includes('текстил')) return 'bed';
        if (n.includes('дім') || n.includes('дому') || n.includes('домів')) return 'home';
        if (n.includes('рослин') || n.includes('сад')) return 'sprout';
        if (n.includes('інструмент') || n.includes('ремонт') || n.includes('будів') || n.includes('послуг')) return 'wrench';
        if (n.includes('крас') || n.includes('здоров') || n.includes('парфум')) return 'sparkles';
        if (n.includes('авто') || n.includes('транспорт') || n.includes('сто')) return 'car';
        if (n.includes('вело')) return 'bike';
        if (n.includes('книг') || n.includes('навча')) return 'bookOpen';
        if (n.includes('дитяч') || n.includes('іграш')) return 'puzzle';
        if (n.includes('спорт') || n.includes('фітнес')) return 'trophy';
        if (n.includes('кемп') || n.includes('туризм')) return 'tent';
        if (n.includes('зоо') || n.includes('корм')) return 'paw';
        if (n.includes('робот') || n.includes('бізнес')) return 'briefcase';
        if (n.includes('перевез')) return 'truck';
        if (n.includes('нерухом')) return 'building';
        if (n.includes('оренд')) return 'key';
        if (n.includes('безкоштов')) return 'gift';
        if (n.includes('обмін')) return 'refresh';
        return 'package';
    }

    function fromCategory(iconOrEmoji, categoryName, opts) {
        const iconName = iconNameFromCategory(iconOrEmoji, categoryName);
        return svg(iconName, opts || { size: 18 });
    }

    function stripEmojis(text) {
        return String(text || '')
            .replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\uFE0F]/gu, '')
            .replace(/\s{2,}/g, ' ')
            .trim();
    }

    global.LoopIcons = {
        svg: svg,
        fromCategory: fromCategory,
        iconNameFromCategory: iconNameFromCategory,
        stripEmojis: stripEmojis,
        has: function (name) { return Boolean(ICONS[name]); }
    };
})(window);
