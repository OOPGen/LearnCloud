/**
 * Library Fast Issue and Return Screen Designed for Barcode Scanner, and Typing When Scanner Fails
 * - Barcode scanner types quickly and sends Enter
 * - Input handles trailing spaces, mixed case, partial matches
 * - Membership derived from learners and staff with configurable borrowing limits and loan periods
 * - Overdue tracking with fines posting to fee account
 */

import React, { useState, useEffect, useRef } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/library";

export default function LibraryFastIssueReturn() {
  const [mode, setMode] = useState("issue"); // issue or return
  const [barcode, setBarcode] = useState("");
  const [memberId, setMemberId] = useState("");
  const [members, setMembers] = useState([]);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const [recentLoans, setRecentLoans] = useState([]);
  const [isScanner, setIsScanner] = useState(false);
  const inputRef = useRef(null);
  const lastKeyTime = useRef(0);

  useEffect(() => {
    // Load members for dropdown (students and staff)
    apiFetch(`${API}/members?search=`, { headers: { Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage } })
      .then(r => r.json()).then(setMembers).catch(() => setMembers([
        {id:1, membershipNumber:"LIB-001", fullName:"Thabo Ndlovu (Grade 5 Blue)", memberType:"student", currentlyBorrowed:1},
        {id:2, membershipNumber:"LIB-002", fullName:"Mrs Moyo (Teacher)", memberType:"teacher", currentlyBorrowed:0}
      ]));

    // Focus input for scanner
    inputRef.current?.focus();

    // Detect scanner vs typing: scanner types very fast (<50ms between keystrokes)
    const handleKeyDown = (e) => {
      const now = Date.now();
      const timeDiff = now - lastKeyTime.current;
      if (timeDiff < 50 && timeDiff > 0) {
        setIsScanner(true);
      } else if (timeDiff > 300) {
        setIsScanner(false);
      }
      lastKeyTime.current = now;
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  async function handleIssue() {
    setError(""); setResult(null);
    if (!barcode.trim()) { setError("Enter barcode or accession number - scanner or typing"); return; }
    if (!memberId) { setError("Select member - membership derived from learners and staff"); return; }

    const res = await apiFetch(`${API}/issue/fast`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage },
      body: JSON.stringify({ barcodeOrAccession: barcode, memberId: parseInt(memberId) })
    });

    const data = await res.json();
    if (res.ok) {
      setResult(data);
      setRecentLoans(prev => [data.loan || data, ...prev].slice(0, 10));
      setBarcode("");
      inputRef.current?.focus();
    } else {
      setError(data.message || "Issue failed");
    }
  }

  async function handleReturn() {
    setError(""); setResult(null);
    if (!barcode.trim()) { setError("Enter barcode or accession number"); return; }

    const res = await apiFetch(`${API}/return/fast`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage },
      body: JSON.stringify({ barcodeOrAccession: barcode, condition: "good" })
    });

    const data = await res.json();
    if (res.ok) {
      setResult(data);
      setRecentLoans(prev => [data.loan || data, ...prev].slice(0, 10));
      setBarcode("");
      inputRef.current?.focus();
      if (data.fineGenerated) {
        alert(`Overdue fine generated: ${data.fineGenerated.amount} ${data.fineGenerated.currency} for ${data.fineGenerated.daysOverdue} days - will post to learner's fee account via existing fee services`);
      }
    } else {
      setError(data.message || "Return failed");
    }
  }

  function handleBarcodeKeyDown(e) {
    if (e.key === "Enter") {
      e.preventDefault();
      if (mode === "issue") handleIssue();
      else handleReturn();
    }
  }

  return (
    <div className="min-h-screen bg-neutral-50 p-4 max-w-5xl mx-auto">
      <h1 className="text-2xl font-bold">Library — Fast Issue and Return for Barcode Scanner</h1>
      <p className="text-xs text-neutral-600 mt-1">Designed for barcode scanner that types quickly + Enter, and typing when scanner fails (trailing spaces, mixed case, partial). Membership derived from learners and staff with configurable borrowing limits and loan periods.</p>

      <div className="mt-4 bg-white border rounded-xl p-4">
        <div className="flex gap-2 mb-4">
          <button onClick={() => setMode("issue")} className={`flex-1 py-3 rounded-lg font-semibold min-h-touch ${mode === "issue" ? "bg-primary-800 text-white" : "bg-neutral-100"}`}>Issue (Borrow)</button>
          <button onClick={() => setMode("return")} className={`flex-1 py-3 rounded-lg font-semibold min-h-touch ${mode === "return" ? "bg-primary-800 text-white" : "bg-neutral-100"}`}>Return</button>
        </div>

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium">Barcode or Accession Number * — Scanner or Typing</label>
            <div className="mt-1 flex gap-2">
              <input
                ref={inputRef}
                value={barcode}
                onChange={e => setBarcode(e.target.value)}
                onKeyDown={handleBarcodeKeyDown}
                placeholder="Scan barcode or type accession e.g. ACC-2026-00001 or BC-12345 — trailing spaces handled"
                className="flex-1 h-12 px-3 border-2 border-primary-200 rounded-lg text-[16px] focus:border-primary-600 focus:ring-0 outline-none"
                autoFocus
              />
              <button onClick={() => setBarcode("")} className="h-12 px-3 rounded-lg border bg-white text-sm">Clear</button>
            </div>
            <div className="mt-1 text-xs text-neutral-500">
              {isScanner ? <span className="text-success-600">✓ Scanner detected (fast typing) — will auto-submit on Enter</span> : <span>Typing mode — handles mixed case, trailing spaces, partial matches when scanner fails</span>}
              <br/>Try: scan barcode gun or type accession number manually — both work
            </div>
          </div>

          {mode === "issue" && (
            <div>
              <label className="block text-sm font-medium">Member — Derived from Learners and Staff, Configurable Limits</label>
              <select value={memberId} onChange={e => setMemberId(e.target.value)} className="mt-1 w-full h-12 px-3 border rounded-lg text-sm">
                <option value="">Select member — membership derived from learners/staff</option>
                {members.map(m => (
                  <option key={m.id} value={m.id}>{m.membershipNumber} — {m.fullName} ({m.memberType}) — Borrowed {m.currentlyBorrowed} — Limit configurable</option>
                ))}
              </select>
              <p className="text-xs text-neutral-500 mt-1">Borrowing limits: student max 3 books 14 days, teacher max 10 books 30 days, fine per day $1.00 — configurable per membership type</p>
            </div>
          )}

          <button
            onClick={mode === "issue" ? handleIssue : handleReturn}
            className="w-full h-12 rounded-full bg-primary-800 text-white font-semibold tracking-wide hover:bg-primary-900 active:scale-[0.98] transition min-h-touch shadow-lg"
          >
            {mode === "issue" ? "Issue Book — Fast" : "Return Book — Fast"}
          </button>

          {error && <div className="p-3 rounded-lg bg-danger-50 border border-danger-200 text-danger-700 text-sm">{error}</div>}

          {result && (
            <div className="p-3 rounded-lg bg-success-50 border border-success-200">
              <div className="font-medium text-success-800">{result.message}</div>
              {result.loan && (
                <div className="mt-2 text-xs">
                  <div>Loan #{result.loan.id} — {result.loan.bookTitle || result.loan.accessionNumber} — Member {result.loan.memberName}</div>
                  <div>Issue {new Date(result.loan.issueDate).toLocaleDateString()} Due {new Date(result.loan.dueDate).toLocaleDateString()} — Status {result.loan.status} {result.loan.isOverdue ? `Overdue ${result.loan.daysOverdue} days` : ""}</div>
                </div>
              )}
              {result.fineGenerated && (
                <div className="mt-2 p-2 rounded bg-warning-50 border border-warning-200 text-xs">
                  <div className="font-medium">Overdue Fine Generated: {result.fineGenerated.amount} {result.fineGenerated.currency} for {result.fineGenerated.daysOverdue} days</div>
                  <div>Fine will post to learner's fee account through existing fee services (FeeInvoice with fee item LIB_FINE) — existing fee entities not altered, consumed via fee service</div>
                </div>
              )}
              <div className="mt-2 text-xs text-neutral-600">Barcode: {result.barcode} • Member: {result.memberId}</div>
            </div>
          )}
        </div>
      </div>

      <div className="mt-4 bg-white border rounded-xl p-4">
        <h3 className="font-semibold text-sm">Recent Transactions — Circulation</h3>
        <div className="mt-2 max-h-80 overflow-auto space-y-1">
          {recentLoans.map((loan, i) => (
            <div key={i} className="p-2 border rounded text-xs flex justify-between">
              <span>{loan.bookTitle || loan.accessionNumber} — {loan.memberName}</span>
              <span className={loan.status === "issued" ? "text-success-600" : "text-neutral-600"}>{loan.status} {loan.dueDate ? `Due ${new Date(loan.dueDate).toLocaleDateString()}` : ""}</span>
            </div>
          ))}
          {recentLoans.length === 0 && <div className="text-xs text-neutral-500">No recent transactions — scan barcode to issue/return</div>}
        </div>
      </div>

      <div className="mt-4 grid md:grid-cols-3 gap-3">
        <div className="bg-white border rounded-lg p-3">
          <h4 className="font-semibold text-xs">Reservations and Waiting List</h4>
          <p className="text-xs text-neutral-600 mt-1">If book reserved by other member, issue blocked, queue position shown. Waiting list: first reserved gets book when returned.</p>
        </div>
        <div className="bg-white border rounded-lg p-3">
          <h4 className="font-semibold text-xs">Overdue Tracking + Fines Posting to Fee Account</h4>
          <p className="text-xs text-neutral-600 mt-1">Overdue detected on return: fine per day $1, max $50, calculates days overdue, creates fine pending, posts to learner's fee account via existing fee services (FeeInvoice with fee item LIB_FINE) without altering existing entities.</p>
        </div>
        <div className="bg-white border rounded-lg p-3">
          <h4 className="font-semibold text-xs">Lost and Damaged + Replacement Charge</h4>
          <p className="text-xs text-neutral-600 mt-1">Lost: status lost, replacement charge = book replacementPrice + overdue fine, posts to fee account. Damaged: condition poor, fine + replacement.</p>
        </div>
      </div>
    </div>
  );
}