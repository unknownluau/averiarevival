import UserAdvertisement from "../userAdvertisement";
import Container from "./components/container";
import SearchUsersStore from "./stores/searchUsersStore";

const SearchUsersNew = props => {
  return <div className='container padding-none'>
    <div className='flex'>
      <div className='col-12 col-lg-12'>
        <div className='mb-4'>
          <UserAdvertisement type={1} />
        </div>
        <SearchUsersStore.Provider>
          <Container keyword={props.keyword} />
        </SearchUsersStore.Provider>
      </div>
    </div>
  </div>;
};

export default SearchUsersNew;
