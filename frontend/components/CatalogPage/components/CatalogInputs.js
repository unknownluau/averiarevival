import {createUseStyles} from "react-jss";
import CatalogPageStore, { CatalogCategory, FormatCamelCase } from "../stores/CatalogPageStore";
import Selector from "../../selector";
import ActionButton from "../../actionButton";
import useButtonStyles from "../../../styles/buttonStyles";
import {IsValidNum, tick, wait} from "../../../lib/utils";
import {useEffect, useState} from "react";
import {getTheme, themeType} from "../../../services/theme";
import {useRouter} from "next/router";

const useStyles = createUseStyles({
    inputStyle: {
        //width: 300,
        width: "100%",
        borderTopRightRadius: 0,
        borderBottomRightRadius: 0,
        fontWeight: 500,
        color: "var(--text-color-primary)",
        "@media(max-width: 576px)": {
            width: "calc(100% - 37px)",
            borderTopLeftRadius: 0,
            borderBottomLeftRadius: 0,
            borderTopRightRadius: 3,
            borderBottomRightRadius: 3,
        },
    },
    selectorWrapper: {
        width: 200,
    },
    selector: {
        padding: "5px 12px",
        borderRadius: 0,
        borderLeft: "none",
        borderColor: "var(--text-color-secondary)",
        "& *": {
            lineHeight: "26px",
            fontWeight: 500,
        },
        "@media(max-width: 576px)": {
            borderLeft: "1px solid var(--text-color-secondary)",
            borderTopLeftRadius: 3,
            borderBottomLeftRadius: 3,
        },
    },
    searchWrapper: {
        width: "calc(100% - 237px)",
        display: "flex",
        "@media(min-width: 768px)": {
            width: 300,
        },
        "@media(max-width: 576px)": {
            justifyContent: "end",
            width: "100%",
            marginBottom: 3,
        },
    },
    searchButton: {
        padding: 4,
        borderTopLeftRadius: 0,
        borderBottomLeftRadius: 0,
        borderLeft: 0,
        // "@media(max-width: 576px)": {
        //     width: "calc(100% - 37px)",
        // },
    },
    btn: {
        padding: 4,
        borderRight: 0,
        borderTopRightRadius: 0,
        borderBottomRightRadius: 0,
        display: "none",
        "@media(max-width: 576px)": {
            display: "block",
        },
    },
    search: {
        "@media(max-width: 576px)": {
            flexDirection: "column",
        },
    },
    sdfaafasfafsaf: {
        "@media(max-width: 576px)": {
            justifyContent: "center",
        },
    },
    iconSearch: {
        filter: p => p.theme === themeType.dark ? "invert(1)" : "none",
    },
    iconMenu: {
        filter: p => p.theme !== themeType.dark ? "invert(1)" : "none",
    },
});

function CatalogInputs() {
    const s = useStyles({theme: getTheme()});
    const store = CatalogPageStore.useContainer();
    const buttonStyles = useButtonStyles();
    const router = useRouter();

    const [deb, setDeb] = useState(false);

    // do we make this useEffect listen to router changes?
    useEffect(() => {
        let cancelled = false;

        async function run() {
            await tick();
            if (cancelled) return;

            let creatorName = router.query['CreatorName'] ?? null;
            let creatorType = router.query['CreatorType'] ?? null;
            let category = router.query['Category'] ?? null;
            let keyword = router.query['keyword'] ?? router.query['Keyword'] ?? null;

            let options = { ...store.options };
            let creatorOpt = store.creator;
            let creatorTypeOpt = store.creatorType;
            let queryOpt = null;

            if (creatorName && typeof creatorName === 'string') creatorOpt = creatorName;
            if (creatorType) {
                if (IsValidNum(creatorType)) {
                    creatorTypeOpt = Number.parseInt(creatorType);
                } else if (typeof creatorType === 'string') {
                    switch (creatorType.toLowerCase()) {
                        case "user": {
                            creatorTypeOpt = 1;
                            break;
                        }
                        case "group": {
                            creatorTypeOpt = 2;
                            break;
                        }
                    }
                }
            }
            if (category && IsValidNum(category)) {
                options.category = Number.parseInt(category);
                if (Number.parseInt(category) === 1) {
                    options.subCategory = 0;
                }
            }
            if (keyword && typeof keyword === 'string' && keyword.trim().length > 0) {
                queryOpt = keyword;
                store.setSearchInput(keyword);
            }
            store.setOptions(options);
            store.setCreator(creatorOpt);
            store.setCreatorType(creatorTypeOpt);

            if (cancelled) return;

            await store.RefreshCatalogItems(null, true, {
                category: options.category,
                subCategory: options.subCategory,
                creator: creatorOpt,
                creatorType: creatorTypeOpt,
                query: queryOpt,
            });
        }

        run().then();

        return () => {
            cancelled = true;
        };
    }, []);
    
    return <div className={`flex ${s.search}`}>
        <div className={s.searchWrapper}>
            <ActionButton
                className={`${s.btn}`}
                buttonStyle={buttonStyles.newCancelButton}
                onClick={e => {
                    e.preventDefault();
                    if (deb) return;
                    setDeb(true);
                    store.setNavVisible(!store.navVisible);
                    wait(0.35).then(() => setDeb(false));
                }}
            >
                <div className="flex justify-content-center align-items-center">
                    <span className={"icon-menu " + s.iconMenu}/>
                </div>
            </ActionButton>
            <input
                type="text"
                placeholder="Search"
                value={store.searchInput || ""}
                onChange={e => store.setSearchInput(e.target.value)}
                className={`inputTextStyle ${store.searchInvalid ? "hasError" : ""} ${s.inputStyle}`}
                maxLength={100}
                onKeyPress={e => {
                    if (e.key === "Enter")
                        store.RefreshCatalogItems(e, true);
                }}
            />
        </div>
        <div className={`flex ${s.sdfaafasfafsaf}`}>
            <Selector
                onChange={async newValue => {
                    if (store.refreshDebounce.current || store.options.category === newValue.value) return false;
                    store.setOptions({...store.options, category: newValue.value});
                    await tick();
                    store.RefreshCatalogItems(null, true, { category: newValue.value });
                }}
                options={Object.keys(CatalogCategory).map(key => (
                    {
                        name: FormatCamelCase(key),
                        value: CatalogCategory[key],
                    }
                ))}
                wrapperClass={s.selectorWrapper}
                className={s.selector}
            />
            <ActionButton
                className={s.searchButton}
                buttonStyle={buttonStyles.newCancelButton}
                onClick={e => store.RefreshCatalogItems(e, true)}
            >
                <div className="flex justify-content-center align-items-center">
                    <span className={`icon-search ${s.iconSearch}`}/>
                </div>
            </ActionButton>
        </div>
    </div>
}

export default CatalogInputs;
