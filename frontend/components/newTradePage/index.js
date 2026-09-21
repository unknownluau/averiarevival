import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/router";
import { createUseStyles } from "react-jss";
import ActionButton from "../actionButton";
import ItemImage from "../itemImage";
import Link from "../NewLink";
import NewModal from "../newModal";
import { counterTrade, createTrade } from "../../services/trades";
import { getCollectibleInventory } from "../../services/inventory";
import { getUserInfo } from "../../services/users";
import AuthenticationStore from "../../stores/authentication";
import FeedbackStore from "../../stores/feedback";
import { FeedbackType } from "../../models/feedback";
import useButtonStyles from "../../styles/buttonStyles";

const categoryOptions = [
  { value: "null", label: "All Accessories" },
  { value: "8", label: "Hats" },
  { value: "41", label: "Hair" },
  { value: "42", label: "Face" },
  { value: "43", label: "Neck" },
  { value: "44", label: "Shoulders" },
  { value: "45", label: "Front" },
  { value: "46", label: "Back" },
  { value: "47", label: "Waist" },
  { value: "19", label: "Gear" },
  { value: "18", label: "Faces" },
];

const formatNumber = value => {
  const parsed = Number(value || 0);
  if (!Number.isFinite(parsed)) return "0";
  return parsed.toLocaleString("en-US");
};

const clampRobuxInput = value => {
  const normalized = value.replace(/[^\d]/g, "");
  if (normalized.length === 0) return "";
  return String(Math.min(parseInt(normalized, 10), 10000000));
};

const getRap = item => Number(item?.recentAveragePrice || 0);

const normalizeTradeItem = item => ({
  ...item,
  userAssetId: item.userAssetId || item.id,
  recentAveragePrice: item.recentAveragePrice ?? item.originalPrice ?? 0,
});

const getErrorMessage = error => {
  const firstError = error?.response?.data?.errors?.[0]?.message;
  if (firstError) return firstError;
  return error?.message || "Unable to send this trade request.";
};

const useStyles = createUseStyles({
  page: {
    width: "100%",
    minHeight: "calc(100vh - 88px)",
    marginTop: -12,
    padding: "24px 24px 68px",
    color: "var(--text-color-primary)",
    background: "transparent",
    fontFamily: "Arial, Helvetica, sans-serif",
    "& *": {
      boxSizing: "border-box",
    },
  },
  shell: {
    width: "100%",
    maxWidth: 1210,
    margin: "0 auto",
  },
  backLink: {
    display: "inline-flex",
    alignItems: "center",
    gap: 6,
    border: 0,
    marginBottom: 10,
    padding: 0,
    background: "transparent",
    color: "var(--text-color-primary)",
    fontSize: 18,
    fontWeight: 600,
    textDecoration: "none",
    "&:hover": {
      color: "var(--primary-color)",
      textDecoration: "none",
    },
  },
  title: {
    margin: "0 0 26px",
    fontSize: 34,
    lineHeight: 1.1,
    fontWeight: 400,
  },
  content: {
    display: "grid",
    gridTemplateColumns: "minmax(0, 704px) minmax(340px, 390px)",
    gap: 28,
    alignItems: "start",
    "@media(max-width: 980px)": {
      gridTemplateColumns: "1fr",
    },
  },
  inventoryColumn: {
    minWidth: 0,
  },
  builderColumn: {
    borderLeft: "1px solid var(--text-color-quinary)",
    paddingLeft: 28,
    "@media(max-width: 980px)": {
      borderLeft: 0,
      paddingLeft: 0,
    },
  },
  inventorySection: {
    paddingBottom: 28,
    marginBottom: 28,
    borderBottom: "1px solid var(--text-color-quinary)",
    "&:last-child": {
      borderBottom: 0,
      marginBottom: 0,
    },
  },
  inventoryHeader: {
    display: "grid",
    gridTemplateColumns: "1fr 280px",
    gap: 18,
    alignItems: "center",
    marginBottom: 20,
    "@media(max-width: 620px)": {
      gridTemplateColumns: "1fr",
    },
  },
  sectionTitle: {
    margin: 0,
    fontSize: 23,
    lineHeight: 1.2,
    fontWeight: 400,
  },
  select: {
    height: 39,
    width: "100%",
    border: "1px solid var(--text-color-secondary)",
    borderRadius: 0,
    background: "var(--white-color)",
    color: "var(--text-color-primary)",
    fontSize: 18,
    fontWeight: 500,
    padding: "0 34px 0 12px",
    outline: 0,
  },
  itemGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(5, minmax(0, 1fr))",
    gap: "16px 9px",
    "@media(max-width: 720px)": {
      gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
    },
    "@media(max-width: 440px)": {
      gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
    },
  },
  itemButton: {
    display: "block",
    width: "100%",
    padding: 0,
    border: 0,
    background: "transparent",
    color: "var(--text-color-primary)",
    textAlign: "left",
    cursor: "pointer",
  },
  itemThumb: {
    position: "relative",
    aspectRatio: "1 / 1",
    width: "100%",
    background: "color-mix(in srgb, var(--text-color-primary) 8%, var(--white-color))",
    border: "1px solid transparent",
    overflow: "hidden",
  },
  itemThumbSelected: {
    borderColor: "var(--text-color-secondary)",
    boxShadow: "inset 0 0 0 2px color-mix(in srgb, var(--text-color-primary) 12%, transparent)",
  },
  itemImage: {
    width: "100%",
    height: "100%",
    objectFit: "contain",
    paddingTop: "0!important",
  },
  serialBadge: {
    position: "absolute",
    left: 7,
    bottom: 6,
    height: 23,
    minWidth: 24,
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    padding: "0 7px",
    borderRadius: 12,
    background: "color-mix(in srgb, var(--text-color-primary) 52%, transparent)",
    color: "var(--white-color)",
    fontSize: 13,
    fontWeight: 500,
  },
  selectedCheck: {
    position: "absolute",
    top: 8,
    right: 8,
    width: 25,
    height: 25,
    borderRadius: 4,
    background: "var(--white-color)",
    border: "1px solid var(--text-color-secondary)",
    color: "var(--text-color-primary)",
    fontSize: 21,
    fontWeight: 400,
    lineHeight: "22px",
    textAlign: "center",
  },
  itemName: {
    minHeight: 42,
    margin: "7px 0 1px",
    color: "var(--text-color-primary)",
    fontSize: 17,
    lineHeight: 1.18,
    fontWeight: 400,
    overflowWrap: "break-word",
  },
  itemValue: {
    display: "flex",
    alignItems: "center",
    gap: 4,
    color: "var(--robux-color)",
    fontSize: 17,
    lineHeight: 1.15,
    fontWeight: 400,
  },
  robuxIcon: {
    display: "inline-block",
    flex: "0 0 auto",
  },
  pagination: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    marginTop: 16,
    fontSize: 18,
    fontWeight: 400,
  },
  pageButton: {
    width: 34,
    height: 34,
    borderRadius: 6,
    border: "1px solid var(--text-color-secondary)",
    background: "color-mix(in srgb, var(--text-color-primary) 10%, var(--white-color))",
    color: "var(--text-color-primary)",
    fontSize: 26,
    lineHeight: "28px",
    padding: 0,
    "&:disabled": {
      opacity: 0.45,
      cursor: "not-allowed",
    },
  },
  feedbackText: {
    minHeight: 120,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: "var(--text-color-secondary)",
    fontSize: 16,
    textAlign: "center",
  },
  offerPanel: {
    marginBottom: 108,
    "&:last-of-type": {
      marginBottom: 20,
    },
  },
  slotStack: {
    display: "grid",
    gap: 16,
  },
  slot: {
    height: 73,
    width: "100%",
    display: "flex",
    alignItems: "center",
    border: "1px dashed var(--text-color-secondary)",
    background: "color-mix(in srgb, var(--text-color-primary) 5%, transparent)",
  },
  slotFilled: {
    borderStyle: "solid",
    background: "color-mix(in srgb, var(--text-color-primary) 7%, var(--white-color))",
  },
  slotContent: {
    width: "100%",
    display: "grid",
    gridTemplateColumns: "52px minmax(0, 1fr)",
    gap: 10,
    alignItems: "center",
    padding: "8px 11px",
    border: 0,
    background: "transparent",
    color: "var(--text-color-primary)",
    textAlign: "left",
    cursor: "pointer",
  },
  slotImageWrap: {
    width: 42,
    height: 42,
    background: "color-mix(in srgb, var(--text-color-primary) 8%, var(--white-color))",
    overflow: "hidden",
  },
  slotImage: {
    width: "100%",
    height: "100%",
    objectFit: "contain",
    paddingTop: "0!important",
  },
  slotName: {
    margin: 0,
    color: "var(--text-color-primary)",
    fontSize: 17,
    lineHeight: 1.15,
    fontWeight: 400,
    whiteSpace: "nowrap",
    overflow: "hidden",
    textOverflow: "ellipsis",
  },
  slotValue: {
    display: "flex",
    alignItems: "center",
    gap: 4,
    marginTop: 2,
    color: "var(--robux-color)",
    fontSize: 16,
    fontWeight: 400,
  },
  robuxInputWrap: {
    position: "relative",
    marginTop: 18,
  },
  inputIcon: {
    position: "absolute",
    top: "50%",
    left: 11,
    transform: "translateY(-50%)",
  },
  robuxInput: {
    width: "100%",
    height: 38,
    padding: "0 12px 0 34px",
    border: "1px solid var(--text-color-quinary)",
    borderRadius: 6,
    background: "color-mix(in srgb, #000 7%, var(--white-color))",
    color: "var(--text-color-primary)",
    fontSize: 17,
    fontWeight: 500,
    outline: 0,
    "&::placeholder": {
      color: "var(--text-color-secondary)",
    },
  },
  feeRow: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    marginTop: 10,
    color: "var(--text-color-secondary)",
    fontSize: 13,
    fontWeight: 600,
  },
  totalRow: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    marginTop: 8,
    color: "var(--text-color-primary)",
    fontSize: 18,
    fontWeight: 400,
  },
  totalValue: {
    display: "flex",
    alignItems: "center",
    gap: 4,
    color: "var(--robux-color)",
    fontSize: 22,
    lineHeight: 1,
  },
  makeOfferButton: {
    width: "100%",
    minHeight: 36,
    marginTop: 18,
    fontSize: "18px!important",
    color: "var(--text-color-primary)!important",
  },
  modalBody: {
    margin: 0,
    color: "var(--text-color-secondary)",
    fontSize: 19,
    lineHeight: 1.25,
  },
  modalButtons: {
    display: "flex",
    justifyContent: "center",
    gap: 12,
    marginTop: 18,
  },
  modalButton: {
    minWidth: 90,
    minHeight: 36,
    padding: "8px 18px!important",
    fontSize: "18px!important",
  },
  error: {
    marginTop: 12,
    color: "var(--danger-color)",
    fontSize: 14,
    textAlign: "center",
  },
  successCenter: {
    textAlign: "center",
  },
  successText: {
    margin: 0,
    color: "var(--text-color-secondary)",
    fontSize: 18,
  },
  backButton: {
    minHeight: 36,
    marginTop: 16,
    padding: "8px 18px!important",
    fontSize: "18px!important",
  },
});

const RobuxValue = ({ value, className }) => {
  const s = useStyles();
  return <span className={className}>
    <span className={`icon-robux-16x16 ${s.robuxIcon}`} />
    <span>{formatNumber(value)}</span>
  </span>;
};

const InventorySection = ({
  title,
  items,
  selectedItems,
  onToggleItem,
  category,
  setCategory,
  page,
  onPrev,
  onNext,
  canPrev,
  canNext,
  loading,
  error,
}) => {
  const s = useStyles();
  const selectedIds = useMemo(() => new Set(selectedItems.map(item => item.userAssetId)), [selectedItems]);

  return <section className={s.inventorySection}>
    <div className={s.inventoryHeader}>
      <h2 className={s.sectionTitle}>{title}</h2>
      <select className={s.select} value={category} onChange={e => setCategory(e.currentTarget.value)}>
        {categoryOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </div>
    {error ? <div className={s.feedbackText}>{error}</div> : loading ? <div className={s.feedbackText}>Loading inventory...</div> : items.length === 0 ? <div className={s.feedbackText}>No limited items in this category.</div> : <div className={s.itemGrid}>
      {items.map(item => {
        const selected = selectedIds.has(item.userAssetId);
        const serial = item.serialNumber || item.serial;
        return <button key={item.userAssetId} type="button" className={s.itemButton} onClick={() => onToggleItem(item)}>
          <div className={`${s.itemThumb} ${selected ? s.itemThumbSelected : ""}`}>
            <ItemImage className={s.itemImage} id={item.assetId} name={item.name} />
            {serial ? <span className={s.serialBadge}>#{serial}</span> : null}
            {selected ? <span className={s.selectedCheck}>{"\u2713"}</span> : null}
          </div>
          <div className={s.itemName}>{item.name}</div>
          <RobuxValue value={getRap(item)} className={s.itemValue} />
        </button>;
      })}
    </div>}
    <div className={s.pagination}>
      <button type="button" className={s.pageButton} disabled={!canPrev} onClick={onPrev}>{"\u2039"}</button>
      <span>Page {page}</span>
      <button type="button" className={s.pageButton} disabled={!canNext} onClick={onNext}>{"\u203a"}</button>
    </div>
  </section>;
};

const TradeSidePanel = ({ title, items, robux, setRobux, onRemoveItem }) => {
  const s = useStyles();
  const parsedRobux = robux ? parseInt(robux, 10) : 0;
  const itemTotal = items.reduce((total, item) => total + getRap(item), 0);
  const total = itemTotal + parsedRobux;
  const afterFee = Math.floor(parsedRobux * 0.7);
  const slots = [...items, ...new Array(Math.max(0, 4 - items.length)).fill(null)];

  return <section className={s.offerPanel}>
    <h2 className={s.sectionTitle}>{title}</h2>
    <div className={s.slotStack}>
      {slots.map((item, index) => <div key={item?.userAssetId || `empty-${title}-${index}`} className={`${s.slot} ${item ? s.slotFilled : ""}`}>
        {item ? <button type="button" className={s.slotContent} onClick={() => onRemoveItem(item)}>
          <span className={s.slotImageWrap}>
            <ItemImage className={s.slotImage} id={item.assetId} name={item.name} />
          </span>
          <span>
            <p className={s.slotName}>{item.name}</p>
            <RobuxValue value={getRap(item)} className={s.slotValue} />
          </span>
        </button> : null}
      </div>)}
    </div>
    <div className={s.robuxInputWrap}>
      <span className={`icon-robux-16x16 ${s.inputIcon}`} />
      <input
        className={s.robuxInput}
        inputMode="numeric"
        value={robux}
        placeholder="Plus Robux amount"
        onChange={e => setRobux(clampRobuxInput(e.currentTarget.value))}
      />
    </div>
    {parsedRobux > 0 ? <div className={s.feeRow}>
      <span>After 30% fee:</span>
      <RobuxValue value={afterFee} className={s.itemValue} />
    </div> : null}
    <div className={s.totalRow}>
      <span>Total Value:</span>
      <RobuxValue value={total} className={s.totalValue} />
    </div>
  </section>;
};

const SendRequestModal = ({ disabled, error, sending, onCancel, onSend, isCounter }) => {
  const s = useStyles();
  const buttonStyles = useButtonStyles();

  return <NewModal title="Send Request" headerBorder exitFunction={onCancel} containerWidth={393} offset={112}>
    <p className={s.modalBody}>Are you sure you want to send {isCounter ? "a counter offer" : "a trade request"}?</p>
    <div className={s.modalButtons}>
      <ActionButton
        label={sending ? "Sending..." : "Send"}
        disabled={disabled || sending}
        buttonStyle={buttonStyles.newBuyButton}
        className={s.modalButton}
        onClick={onSend}
      />
      <ActionButton
        label="Cancel"
        disabled={sending}
        buttonStyle={buttonStyles.newCancelButton}
        className={s.modalButton}
        onClick={onCancel}
      />
    </div>
    {error ? <div className={s.error}>{error}</div> : null}
  </NewModal>;
};

const TradeSentModal = ({ onBack }) => {
  const s = useStyles();
  const buttonStyles = useButtonStyles();

  return <NewModal title="Trade Sent!" headerBorder exitFunction={onBack} containerWidth={394} offset={110}>
    <div className={s.successCenter}>
      <p className={s.successText}>Your trade request has been sent!</p>
      <ActionButton
        label="Back to Trades List"
        buttonStyle={buttonStyles.newCancelButton}
        className={s.backButton}
        onClick={onBack}
      />
    </div>
  </NewModal>;
};

const useInventory = ({ userId, category, pageResetKey }) => {
  const [items, setItems] = useState([]);
  const [cursor, setCursor] = useState("");
  const [page, setPage] = useState(1);
  const [nextCursor, setNextCursor] = useState(null);
  const [prevCursor, setPrevCursor] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setCursor("");
    setPage(1);
  }, [category, pageResetKey]);

  useEffect(() => {
    if (!userId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getCollectibleInventory({ userId, cursor, limit: 10, assetTypeId: category }).then(result => {
      if (cancelled) return;
      setItems(Array.isArray(result?.data) ? result.data : []);
      setNextCursor(result?.nextPageCursor || null);
      setPrevCursor(result?.previousPageCursor || null);
    }).catch(error => {
      if (cancelled) return;
      setItems([]);
      if (error.response?.status === 403) {
        setError("This player's inventory is private.");
      } else {
        setError(getErrorMessage(error));
      }
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });
    return () => {
      cancelled = true;
    };
  }, [userId, cursor, category]);

  return {
    items,
    page,
    loading,
    error,
    canPrev: Boolean(prevCursor),
    canNext: Boolean(nextCursor),
    goPrev: () => {
      if (!prevCursor) return;
      setCursor(prevCursor);
      setPage(current => Math.max(1, current - 1));
    },
    goNext: () => {
      if (!nextCursor) return;
      setCursor(nextCursor);
      setPage(current => current + 1);
    },
  };
};

const NewTradePage = ({ counterTrade: counterTradeDetails, partnerUserId: partnerUserIdOverride, onBack, onTradeSent } = {}) => {
  const s = useStyles();
  const buttonStyles = useButtonStyles();
  const router = useRouter();
  const auth = AuthenticationStore.useContainer();
  const feedback = FeedbackStore.useContainer();
  const partnerUserId = partnerUserIdOverride || router.query.userId;

  const [partner, setPartner] = useState(null);
  const [partnerError, setPartnerError] = useState(null);
  const [myCategory, setMyCategory] = useState("null");
  const [partnerCategory, setPartnerCategory] = useState("null");
  const [offerItems, setOfferItems] = useState([]);
  const [requestItems, setRequestItems] = useState([]);
  const [offerRobux, setOfferRobux] = useState("");
  const [requestRobux, setRequestRobux] = useState("");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [sentOpen, setSentOpen] = useState(false);
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState(null);

  const myInventory = useInventory({ userId: auth.userId, category: myCategory, pageResetKey: auth.userId });
  const partnerInventory = useInventory({ userId: partnerUserId, category: partnerCategory, pageResetKey: partnerUserId });

  useEffect(() => {
    if (!counterTradeDetails?.offers || !auth.userId) return;
    const authenticatedOffer = counterTradeDetails.offers.find(offer => offer.user?.id === auth.userId);
    const otherOffer = counterTradeDetails.offers.find(offer => offer.user?.id !== auth.userId);
    setOfferItems((authenticatedOffer?.userAssets || []).map(normalizeTradeItem));
    setRequestItems((otherOffer?.userAssets || []).map(normalizeTradeItem));
    setOfferRobux(authenticatedOffer?.robux ? String(authenticatedOffer.robux) : "");
    setRequestRobux(otherOffer?.robux ? String(otherOffer.robux) : "");
  }, [counterTradeDetails?.id, auth.userId]);

  useEffect(() => {
    if (!partnerUserId) return;
    let cancelled = false;
    setPartner(null);
    setPartnerError(null);
    getUserInfo({ userId: partnerUserId }).then(result => {
      if (!cancelled) setPartner(result);
    }).catch(error => {
      if (!cancelled) setPartnerError(getErrorMessage(error));
    });
    return () => {
      cancelled = true;
    };
  }, [partnerUserId]);

  const toggleOfferItem = item => {
    setOfferItems(current => {
      if (current.find(selected => selected.userAssetId === item.userAssetId)) {
        return current.filter(selected => selected.userAssetId !== item.userAssetId);
      }
      if (current.length >= 4) return current;
      return [...current, item];
    });
  };

  const toggleRequestItem = item => {
    setRequestItems(current => {
      if (current.find(selected => selected.userAssetId === item.userAssetId)) {
        return current.filter(selected => selected.userAssetId !== item.userAssetId);
      }
      if (current.length >= 4) return current;
      return [...current, item];
    });
  };

  const hasRequiredItems = offerItems.length > 0 && requestItems.length > 0;
  const isSelfTrade = auth.userId && partnerUserId && Number(auth.userId) === Number(partnerUserId);
  const canMakeOffer = auth.isAuthenticated && !isSelfTrade && hasRequiredItems;

  const makeOfferTooltip = !auth.isAuthenticated
    ? "You need to be logged in to trade."
    : isSelfTrade
      ? "You cannot trade with yourself."
      : !hasRequiredItems
        ? "Select at least one item from each inventory."
        : "";

  const sendTradeRequest = async () => {
    if (!canMakeOffer || sending) return;
    setSending(true);
    setSendError(null);
    try {
      const request = {
        offerUserId: auth.userId,
        requestUserId: Number(partnerUserId),
        offerUserAssets: offerItems.map(item => item.userAssetId),
        requestUserAssets: requestItems.map(item => item.userAssetId),
        offerRobux: offerRobux ? parseInt(offerRobux, 10) : null,
        requestRobux: requestRobux ? parseInt(requestRobux, 10) : null,
      };
      if (counterTradeDetails?.id) {
        await counterTrade({
          tradeId: counterTradeDetails.id,
          ...request,
        });
      } else {
        await createTrade(request);
      }
      setConfirmOpen(false);
      setSentOpen(true);
      feedback.addFeedback("Successfully sent trade request.", FeedbackType.SUCCESS, true);
      if (onTradeSent) onTradeSent();
    } catch (error) {
      setSendError(getErrorMessage(error));
    } finally {
      setSending(false);
    }
  };

  const partnerName = partner?.name || "User";
  const pageTitle = counterTradeDetails?.id
    ? `Counter Trade with ${partnerError ? "User" : partnerName}`
    : `Trade with ${partnerError ? "User" : partnerName}`;
  const goBack = () => {
    if (onBack) {
      onBack();
      return;
    }
    router.push("/trades");
  };

  return <div className={s.page}>
    <div className={s.shell}>
      {onBack ? <button type="button" className={s.backLink} onClick={onBack}>
        <span>‹</span>
        <span>Back to Trades List</span>
      </button> : <Link href="/trades" className={s.backLink}>
        <span>‹</span>
        <span>Back to Trades List</span>
      </Link>}
      <h1 className={s.title}>{pageTitle}</h1>
      {partnerError ? <div className={s.feedbackText}>{partnerError}</div> : <div className={s.content}>
        <div className={s.inventoryColumn}>
          <InventorySection
            title="Your Inventory"
            items={myInventory.items}
            selectedItems={offerItems}
            onToggleItem={toggleOfferItem}
            category={myCategory}
            setCategory={setMyCategory}
            page={myInventory.page}
            onPrev={myInventory.goPrev}
            onNext={myInventory.goNext}
            canPrev={myInventory.canPrev}
            canNext={myInventory.canNext}
            loading={auth.isPending || myInventory.loading}
            error={myInventory.error}
          />
          <InventorySection
            title={`${partnerName}'s Inventory`}
            items={partnerInventory.items}
            selectedItems={requestItems}
            onToggleItem={toggleRequestItem}
            category={partnerCategory}
            setCategory={setPartnerCategory}
            page={partnerInventory.page}
            onPrev={partnerInventory.goPrev}
            onNext={partnerInventory.goNext}
            canPrev={partnerInventory.canPrev}
            canNext={partnerInventory.canNext}
            loading={!partner || partnerInventory.loading}
            error={partnerInventory.error}
          />
        </div>
        <aside className={s.builderColumn}>
          <TradeSidePanel
            title="Your Offer"
            items={offerItems}
            robux={offerRobux}
            setRobux={setOfferRobux}
            onRemoveItem={item => setOfferItems(current => current.filter(selected => selected.userAssetId !== item.userAssetId))}
          />
          <TradeSidePanel
            title="Your Request"
            items={requestItems}
            robux={requestRobux}
            setRobux={setRequestRobux}
            onRemoveItem={item => setRequestItems(current => current.filter(selected => selected.userAssetId !== item.userAssetId))}
          />
          <ActionButton
            label="Make Offer"
            disabled={!canMakeOffer}
            tooltipText={makeOfferTooltip}
            buttonStyle={!canMakeOffer ? `${buttonStyles.newCancelButton} ${buttonStyles.newDisabledCancelButton}` : buttonStyles.newCancelButton}
            className={s.makeOfferButton}
            onClick={() => {
              setSendError(null);
              setConfirmOpen(true);
            }}
          />
        </aside>
      </div>}
    </div>
    {confirmOpen ? <SendRequestModal
      disabled={!canMakeOffer}
      sending={sending}
      error={sendError}
      onCancel={() => !sending && setConfirmOpen(false)}
      onSend={sendTradeRequest}
      isCounter={Boolean(counterTradeDetails?.id)}
    /> : null}
    {sentOpen ? <TradeSentModal onBack={goBack} /> : null}
  </div>;
};

export default NewTradePage;
