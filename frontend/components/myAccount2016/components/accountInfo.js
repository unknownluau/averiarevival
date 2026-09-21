import React, { useRef } from "react";
import { createUseStyles } from "react-jss";
import getFlag from "../../../lib/getFlag";
import { setUserDescription } from "../../../services/accountInformation";
import {
    getAvPageStyle, getCatalogPageStyle, getCatalogStyle, getTradeStyle,
    getGroupPagesStyle, getHideRE, getSearchUserPageStyle, getTheme, setAvPageStyle,
    setCatalogPageStyle, setCatalogStyle, setTradeStyle, 
    setGroupPagesStyle, setHideRE, setSearchUserPageStyle,
    setTheme, setThemeColor, setThemeCustomColor, setThemeForumHeader,
    setThemeRibbon, themeType
} from "../../../services/theme";
import AuthenticationStore from "../../../stores/authentication";
import useCardStyles from "../../userProfile/styles/card";
import MyAccountStore from "../stores/myAccountStore"
import useFormStyles from "../styles/forms";
import Subtitle from "./subtitle";
import { setAvPageStyleReq } from "../../../services/accountSettings";
import { ChangeVarsForThemeColor, ChangeVarsForThemeFont } from "../../../lib/ThemeUtil";

const useEditButtonStyles = createUseStyles({
    editButton: {
        float: 'right',
        color: 'var(--text-color-primary)',
        cursor: 'pointer',
    }
})

const EditButton = (props) => {
    const s = useEditButtonStyles();
    return <span className={s.editButton} onClick={props.onClick}>Edit</span>
}

const  AccountInfo = props => {
    const store = MyAccountStore.useContainer();
    const auth = AuthenticationStore.useContainer();
    const descRef = useRef(null);
    
    const isDebug = false;
    const year = new Date().getFullYear();
    const christmasStart = new Date(year, 10, 28);
    const christmasEnd = new Date(year, 12, 31);
    const halloweenStart = new Date(year, 9, 1);
    const halloweenEnd = new Date(year, 10, 5);
    
    const cardStyles = useCardStyles();
    const s = useFormStyles();
    return <div className='row'>
        <div className='col-12 mt-2'>
            <Subtitle>Account Info</Subtitle>
            <div className={cardStyles.card + ' p-3'}>
                <p className={s.accountInfoLabel}>Username: <span className={s.accountInfoValue}>{auth.username}</span>
                    <EditButton onClick={() => {
                        store.setModal('CHANGE_USERNAME');
                    }}></EditButton></p>
                <p className={s.accountInfoLabel}>Password: <span className={s.accountInfoValue}>**********</span>
                    <EditButton onClick={() => {
                        store.setModal('CHANGE_PASSWORD');
                    }}></EditButton></p>
                <p className={s.accountInfoLabel}>Email Address: <span
                    className={s.accountInfoValue}>{store.email}</span> <EditButton onClick={() => {
                    store.setModal('CHANGE_EMAIL');
                }}></EditButton></p>
            </div>
        </div>
        <div className='col-12 mt-2'>
            <Subtitle>Personal</Subtitle>
            <div className={cardStyles.card + ' p-3'}>
                <textarea ref={descRef} className={s.descInput} rows={3} defaultValue={store.description}></textarea>
                <p className='mb-0 font-size-12'>Do not provide any details that can be used to identify you outside
                    Averia.</p>
                <div className='mt-1'>
                    <div className='row'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Birthday'
                                   readOnly={true} type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select}>
                                <option value='1'>January</option>
                                <option value='2'>February</option>
                                <option value='3'>March</option>
                                <option value='4'>April</option>
                                <option value='5'>May</option>
                                <option value='6'>June</option>
                                <option value='7'>July</option>
                                <option value='8'>August</option>
                                <option value='9'>September</option>
                                <option value='10'>October</option>
                                <option value='11'>November</option>
                                <option value='12'>December</option>
                            </select>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select}>
                                {[...new Array(31)].map((v, i) => {
                                    return <option value={i + 1} key={i}>{i + 1}</option>
                                })}
                            </select>
                        </div>
                        <div className='col ps-0'>
                            <select className={'form-control ' + s.select}>
                                {[...new Array(100)].map((v, i) => {
                                    return <option value={2016 - i} key={i}>{2016 - i}</option>
                                })}
                            </select>
                        </div>
                    </div>
                </div>
                <div className='mt-1 mb-4'>
                    <div className={s.saveButtonWrapper}>
                        <button className={s.saveButton} onClick={() => {
                            // todo: gender, birthdate
                            setUserDescription({
                                newDescription: descRef.current.value,
                            });
                        }}>Save
                        </button>
                    </div>
                </div>
                <div className='mt-4 mb-4'>&emsp;</div>
            </div>
        </div>
        {getFlag('settingsPageThemeSelectorEnabled', false) &&
            <div className='col-12 mt-2'>
                <Subtitle>Customize Your Averia</Subtitle>
                <div className={cardStyles.card + ' p-3'}>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Website Theme'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={store.theme.theme} onChange={(ev) => {
                                setTheme(ev.currentTarget.value);
                                store.setTheme({
                                    ...store.theme,
                                    theme: ev.target.value,
                                });
                                window.location.reload();
                            }}>
                                <option value='light'>Light Theme</option>
                                <option value='obc2019'>OBC Theme</option>
                                <option value='dark'>Dark Theme (BETA)</option>
                            </select>
                        </div>
                    </div>
                    {
                        getTheme() === themeType.dark || getTheme() === themeType.obc2019
                        ?
                        <div className='flex mt-1'>
                            <div className='col pe-0'>
                                <input className={'form-control ' + s.select + ' ' + s.disabled} value='Apply Theme to Ribbonbar'
                                       readOnly={true}
                                       type='text'></input>
                            </div>
                            <div className='col ps-0 pe-0'>
                                <select className={'form-control ' + s.select} value={store.theme.themeRibbon} onChange={(ev) => {
                                    setThemeRibbon(ev.currentTarget.value);
                                    store.setTheme({
                                        ...store.theme,
                                        themeRibbon: ev.target.value,
                                    });
                                    window.location.reload();
                                }}>
                                    <option value='true'>Yes</option>
                                    <option value='false'>No</option>
                                </select>
                            </div>
                        </div>
                        :
                        null
                    }
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Website Color'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={store.theme.color} onChange={(ev) => {
                                setThemeColor(ev.currentTarget.value);
                                ChangeVarsForThemeColor(ev.currentTarget.value);
                                store.setTheme({
                                    ...store.theme,
                                    color: ev.target.value,
                                });
                            }}>
                                {
                                    isDebug || year >= christmasStart && year <= christmasEnd ? <option value='cane'>Cane</option> : null
                                }
                                {
                                    isDebug || year >= halloweenStart && year <= halloweenEnd ? <option value='spice'>Spice</option> : null
                                }
                                <option value='coffee'>Coffee</option>
                                <option value='bliss'>Bliss</option>
                                <option value='cobalt'>Cobalt</option>
                                <option value='sunlit'>Sunlit</option>
                                <option value='royalty'>Royalty</option>
                                <option value='nobility'>Nobility (zyth's color :3)</option>
                                <option value='harmony'>Harmony</option>
                                <option value='witness'>Witness</option>
                                <option value='whisper'>Velvet Whisper</option>
                                <option value='custom'>Custom</option>
                            </select>
                        </div>
                    </div>
                    {store.theme.color === 'custom' &&
                        <div className='flex mt-1'>
                            <div className='col pe-0'>
                                <input className={'form-control ' + s.select + ' ' + s.disabled} value='Custom Website Color'
                                       readOnly={true}
                                       type='text'></input>
                            </div>
                            <div className='col ps-0 pe-0'>
                                <label className={'form-control ' + s.select + ' ' + s.disabled + ' ' + s.customColorRow}>
                                    <input type='color'
                                           className={s.customColorSwatch}
                                           value={store.theme.customColor || '#000000'}
                                           onChange={(ev) => {
                                               const hexColor = ev.currentTarget.value;
                                               setThemeCustomColor(hexColor);
                                               ChangeVarsForThemeColor('custom', hexColor);
                                               store.setTheme({
                                                   ...store.theme,
                                                   customColor: hexColor,
                                               });
                                           }}/>
                                    <span className={s.customColorHex}>{store.theme.customColor || 'Pick a color'}</span>
                                </label>
                            </div>
                        </div>
                    }
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Stylize Forum Headers'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={store.theme.stylizeForumHeader} onChange={(ev) => {
                                setThemeForumHeader(ev.currentTarget.value);
                                store.setTheme({
                                    ...store.theme,
                                    stylizeForumHeader: ev.target.value,
                                });
                            }}>
                                <option value='true'>Yes</option>
                                <option value='false'>No</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Group Page Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getGroupPagesStyle()}
                                    onChange={ev => {
                                        setGroupPagesStyle(ev.currentTarget.value);
                                        window.location.replace(window.location.pathname + '?t=' + new Date().getTime());
                                    }}>
                                <option value="Modern">Modern (2018-2024)</option>
                                <option value="Legacy">Legacy (2016-2018)</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Search Player Page'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getSearchUserPageStyle()}
                                    onChange={ev => {
                                        setSearchUserPageStyle(ev.currentTarget.value);
                                        window.location.replace(window.location.pathname + '?t=' + new Date().getTime());
                                    }}>
                                <option value="Modern">Modern (2018+)</option>
                                <option value="Legacy">Legacy (2017)</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Catalog Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getCatalogStyle()}
                                    onChange={ev => {
                                        setCatalogStyle(ev.currentTarget.value);
                                        window.location.replace(window.location.pathname + '?t=' + new Date().getTime());
                                    }}>
                                <option value="Modern">Modern (2017+)</option>
                                <option value="Legacy">Legacy (2012-2017)</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Trade Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getTradeStyle()}
                                    onChange={ev => {
                                        setTradeStyle(ev.currentTarget.value);
                                        window.location.replace(window.location.pathname + '?t=' + new Date().getTime());
                                    }}>
                                <option value="Modern">Modern (2020+)</option>
                                <option value="Legacy">Legacy (2012-2019)</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Avatar Page Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getAvPageStyle()} onChange={(ev) => {
                                setAvPageStyle(ev.currentTarget.value);
                                setAvPageStyleReq({ newAvatarPageStyle: ev.currentTarget.value }).then();
                                window.location.reload();
                            }}>
                                <option value="Modern">Modern (2017+)</option>
                                <option value="Legacy">Legacy (2013-2017)</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Asset Details Page Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getCatalogPageStyle()}
                                    onChange={ev => {
                                        setCatalogPageStyle(ev.currentTarget.value);
                                        window.location.reload();
                                    }}>
                                <option value="Modern">Modern (2017+)</option>
                                <option value="Legacy">Legacy (2012-2017)</option>
                            </select>
                        </div>
                    </div>
                    <div className='flex mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Hide Ragdoll Engine'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getHideRE()}
                                    onChange={ev => {
                                        setHideRE(ev.currentTarget.value);
                                        window.location.reload();
                                    }}>
                                <option value="Yes">Yes</option>
                                <option value="No">No</option>
                            </select>
                        </div>
                    </div>
                </div>
            </div>
        }
    </div>
}

export default AccountInfo;